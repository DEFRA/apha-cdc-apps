using System.Globalization;
using Microsoft.Playwright;

namespace CDC.Web.Infrastructure.Pdf;

/// <summary>
/// Renders HTML to PDF with Playwright's headless Chromium. One browser is shared for the
/// application's lifetime and each render gets its own isolated context; concurrent renders are
/// capped because Chromium is memory-hungry.
/// </summary>
/// <param name="configuration">Supplies <c>PdfGeneration:MaxConcurrentRenders</c> (default 2).</param>
/// <param name="logger">Structured logger.</param>
public sealed partial class PlaywrightPdfService(IConfiguration configuration, ILogger<PlaywrightPdfService> logger)
    : IPlaywrightPdfService, IAsyncDisposable
{
    private static readonly byte[] PdfSignature = "%PDF"u8.ToArray();

    private readonly SemaphoreSlim renderSlots = new(
        Math.Max(1, configuration.GetValue("PdfGeneration:MaxConcurrentRenders", 2)));

    private readonly SemaphoreSlim browserLock = new(1, 1);
    private IPlaywright? playwright;
    private IBrowser? browser;

    /// <inheritdoc />
    public async Task<byte[]> RenderAsync(string html, PdfRenderOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);
        ArgumentNullException.ThrowIfNull(options);

        await renderSlots.WaitAsync(cancellationToken);

        try
        {
            var chromium = await GetBrowserAsync(cancellationToken);
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
        catch (Exception exception) when (exception is PlaywrightException or TimeoutException)
        {
            LogRenderFailed(exception);

            throw new PdfRenderException("The PDF could not be rendered. Check that the Playwright Chromium browser is installed.", exception);
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
            await browser.DisposeAsync();
        }

        playwright?.Dispose();
        renderSlots.Dispose();
        browserLock.Dispose();
    }

    private static PagePdfOptions BuildPdfOptions(PdfRenderOptions options)
    {
        var displayHeaderFooter = options.HeaderHtml is not null || options.FooterHtml is not null;

        return new PagePdfOptions
        {
            Format = "A4",
            PrintBackground = true,
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
        if (browser is not null)
        {
            return browser;
        }

        await browserLock.WaitAsync(cancellationToken);

        try
        {
            if (browser is null)
            {
                playwright = await Playwright.CreateAsync();
                browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = true,
                    Channel = configuration["PdfGeneration:BrowserChannel"]
                });
                LogBrowserStarted();
            }

            return browser;
        }
        finally
        {
            browserLock.Release();
        }
    }

    [LoggerMessage(EventId = 2600, Level = LogLevel.Information, Message = "Playwright Chromium started for PDF rendering")]
    private partial void LogBrowserStarted();

    [LoggerMessage(EventId = 2601, Level = LogLevel.Error, Message = "PDF rendering failed")]
    private partial void LogRenderFailed(Exception exception);
}
