using System.Globalization;
using Microsoft.Playwright;

namespace CDC.Web.Infrastructure.Pdf;

/// <summary>
/// Renders HTML to PDF with Playwright's headless Chromium. One browser is shared for the
/// application's lifetime and each render gets its own isolated context; concurrent renders are
/// capped because Chromium is memory-hungry. A browser that has exited is replaced by a new one.
/// </summary>
public sealed partial class PlaywrightPdfService : IPlaywrightPdfService, IAsyncDisposable
{
    private static readonly byte[] PdfSignature = "%PDF"u8.ToArray();

    private readonly ILogger<PlaywrightPdfService> logger;
    private readonly Func<CancellationToken, Task<IBrowser>> launchBrowser;
    private readonly SemaphoreSlim renderSlots;
    private readonly SemaphoreSlim browserLock = new(1, 1);
    private IPlaywright? playwright;
    private IBrowser? browser;

    /// <summary>Initialises a new instance of the <see cref="PlaywrightPdfService"/> class.</summary>
    /// <param name="configuration">Supplies <c>PdfGeneration:MaxConcurrentRenders</c> (default 2).</param>
    /// <param name="logger">Structured logger.</param>
    public PlaywrightPdfService(IConfiguration configuration, ILogger<PlaywrightPdfService> logger)
        : this(configuration, logger, launchBrowser: null)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="PlaywrightPdfService"/> class that starts its browser with
    /// <paramref name="launchBrowser"/>, or with Playwright's Chromium when that is <see langword="null"/>.</summary>
    internal PlaywrightPdfService(
        IConfiguration configuration,
        ILogger<PlaywrightPdfService> logger,
        Func<CancellationToken, Task<IBrowser>>? launchBrowser)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        this.logger = logger;
        this.launchBrowser = launchBrowser ?? (_ => LaunchChromiumAsync());
        renderSlots = new(Math.Max(1, configuration.GetValue("PdfGeneration:MaxConcurrentRenders", 2)));
    }

    /// <inheritdoc />
    public async Task<byte[]> RenderAsync(string html, PdfRenderOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);
        ArgumentNullException.ThrowIfNull(options);

        await renderSlots.WaitAsync(cancellationToken);

        try
        {
            var chromium = await GetBrowserAsync(cancellationToken);

            try
            {
                return await PrintAsync(chromium, html, options);
            }
            catch (PlaywrightException exception)
            {
                // Chromium can exit while still reported as connected, which shows up here as a Playwright error
                // (its TargetClosedException is internal), so one new browser gets a second try.
                LogBrowserClosed(exception);
                await DiscardBrowserAsync(chromium, cancellationToken);

                return await PrintAsync(await GetBrowserAsync(cancellationToken), html, options);
            }
        }
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            LogRenderFailed(exception);

            throw new PdfRenderException("The PDF could not be rendered by the browser.", exception);
        }
        finally
        {
            renderSlots.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (browser is not null)
        {
            await CloseQuietlyAsync(browser);
        }

        playwright?.Dispose();
        renderSlots.Dispose();
        browserLock.Dispose();
    }

    private static async Task<byte[]> PrintAsync(IBrowser chromium, string html, PdfRenderOptions options)
    {
        await using var context = await chromium.NewContextAsync();
        var page = await context.NewPageAsync();

        await page.SetContentAsync(html, new PageSetContentOptions { WaitUntil = WaitUntilState.Load });

        var pdf = await page.PdfAsync(BuildPdfOptions(options));

        if (pdf.Length < PdfSignature.Length || !pdf.AsSpan(0, PdfSignature.Length).SequenceEqual(PdfSignature))
        {
            throw new PdfRenderException("The browser returned data that is not a PDF.");
        }

        return pdf;
    }

    private static PagePdfOptions BuildPdfOptions(PdfRenderOptions options)
    {
        var displayHeaderFooter = options.HeaderHtml is not null || options.FooterHtml is not null;

        return new PagePdfOptions
        {
            Format = "A4",
            PrintBackground = true,
            Outline = options.GenerateOutline,
            PreferCSSPageSize = options.PreferCssPageSize,
            DisplayHeaderFooter = displayHeaderFooter,
            HeaderTemplate = options.HeaderHtml ?? "<span></span>",
            FooterTemplate = options.FooterHtml ?? "<span></span>",
            Margin = new Margin
            {
                Top = Millimetres(options.MarginTopMm),
                Bottom = Millimetres(options.MarginBottomMm),
                Left = Millimetres(options.MarginLeftMm),
                Right = Millimetres(options.MarginRightMm)
            }
        };
    }

    private static string Millimetres(double value) => string.Create(CultureInfo.InvariantCulture, $"{value}mm");

    private async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
    {
        if (browser is { IsConnected: true } running)
        {
            return running;
        }

        await browserLock.WaitAsync(cancellationToken);

        try
        {
            if (browser is { IsConnected: true } started)
            {
                return started;
            }

            if (browser is not null)
            {
                LogBrowserRestarting();
                await CloseQuietlyAsync(browser);
            }

            browser = await launchBrowser(cancellationToken);
            LogBrowserStarted();

            return browser;
        }
        finally
        {
            browserLock.Release();
        }
    }

    // Only the browser that failed is dropped; another render may already have replaced it.
    private async Task DiscardBrowserAsync(IBrowser failed, CancellationToken cancellationToken)
    {
        await browserLock.WaitAsync(cancellationToken);

        try
        {
            if (ReferenceEquals(browser, failed))
            {
                browser = null;
                await CloseQuietlyAsync(failed);
            }
        }
        finally
        {
            browserLock.Release();
        }
    }

    // A browser that has already exited may refuse to close; that must not stop a new one starting.
    private async Task CloseQuietlyAsync(IBrowser closing)
    {
        try
        {
            await closing.DisposeAsync();
        }
        catch (PlaywrightException exception)
        {
            LogBrowserCloseFailed(exception);
        }
    }

    private async Task<IBrowser> LaunchChromiumAsync()
    {
        playwright ??= await Playwright.CreateAsync();

        return await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            Channel = "chromium"
        });
    }

    [LoggerMessage(EventId = 2600, Level = LogLevel.Information, Message = "Playwright Chromium started for PDF rendering")]
    private partial void LogBrowserStarted();

    [LoggerMessage(EventId = 2601, Level = LogLevel.Error, Message = "PDF rendering failed")]
    private partial void LogRenderFailed(Exception exception);

    [LoggerMessage(EventId = 2602, Level = LogLevel.Warning, Message = "The PDF browser had exited; starting a new one")]
    private partial void LogBrowserRestarting();

    [LoggerMessage(EventId = 2603, Level = LogLevel.Warning, Message = "The PDF browser failed during a render; retrying with a new browser")]
    private partial void LogBrowserClosed(Exception exception);

    [LoggerMessage(EventId = 2604, Level = LogLevel.Warning, Message = "The PDF browser could not be closed")]
    private partial void LogBrowserCloseFailed(Exception exception);
}
