using Microsoft.Playwright;

namespace CDC.Web.Infrastructure;

/// <summary>Converts a self-contained HTML document to a PDF.</summary>
public interface IPdfRenderer
{
    /// <param name="html">A complete HTML document with inline styles; it must not need network access.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The PDF bytes.</returns>
    Task<byte[]> RenderLandscapeAsync(string html, CancellationToken cancellationToken = default);
}

/// <summary>
/// Renders PDFs with headless Chromium through Playwright. The browser is started on first use
/// and reused; each render gets its own context, with scripting and network access disabled
/// because the HTML includes stored profile content. <c>Pdf:ChromiumExecutablePath</c> selects an
/// installed Chromium-based browser (for example Edge) instead of the downloaded Chromium.
/// </summary>
public sealed class PlaywrightPdfRenderer(IConfiguration configuration) : IPdfRenderer, IAsyncDisposable
{
    private const string FooterTemplate =
        "<div style=\"width:100%;font-size:9px;font-family:Arial,sans-serif;text-align:center\">Page <span class=\"pageNumber\"></span> of <span class=\"totalPages\"></span></div>";

    private readonly SemaphoreSlim startLock = new(1, 1);
    private IPlaywright? playwright;
    private IBrowser? browser;

    /// <inheritdoc />
    public async Task<byte[]> RenderLandscapeAsync(string html, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);

        var chromium = await GetBrowserAsync(cancellationToken);

        await using var context = await chromium.NewContextAsync(new BrowserNewContextOptions { JavaScriptEnabled = false });
        await context.RouteAsync("http*://**", route => route.AbortAsync());

        var page = await context.NewPageAsync();
        await page.SetContentAsync(html, new PageSetContentOptions { WaitUntil = WaitUntilState.Load });

        return await page.PdfAsync(new PagePdfOptions
        {
            Format = "A4",
            Landscape = true,
            PrintBackground = true,
            DisplayHeaderFooter = true,
            HeaderTemplate = "<span></span>",
            FooterTemplate = FooterTemplate,
            Margin = new Margin { Top = "15mm", Bottom = "18mm", Left = "10mm", Right = "10mm" }
        });
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (browser is not null)
        {
            await browser.CloseAsync();
        }

        playwright?.Dispose();
        startLock.Dispose();
    }

    private async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
    {
        await startLock.WaitAsync(cancellationToken);

        try
        {
            if (browser is { IsConnected: true })
            {
                return browser;
            }

            playwright ??= await Playwright.CreateAsync();
            browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                ExecutablePath = configuration["Pdf:ChromiumExecutablePath"]
            });

            return browser;
        }
        finally
        {
            startLock.Release();
        }
    }
}
