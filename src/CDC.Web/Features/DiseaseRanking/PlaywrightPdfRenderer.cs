using System.Net;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace CDC.Web.Features.DiseaseRanking;

/// <summary>Configuration for <see cref="PlaywrightPdfRenderer"/>, bound from the <c>Pdf</c> section.</summary>
public sealed class PdfRendererOptions
{
    /// <summary>The configuration section name.</summary>
    public const string SectionName = "Pdf";

    /// <summary>
    /// Gets or sets the path of a Chromium/Chrome executable to use instead of Playwright's own
    /// download. Leave empty to use the browser installed with <c>playwright install chromium</c>.
    /// </summary>
    public string? ChromiumExecutablePath { get; set; }
}

/// <summary>
/// Renders HTML to PDF with headless Chromium through Playwright, replacing the legacy TallPDF
/// layout engine. One browser is started on first use and shared; each render uses its own isolated
/// page with JavaScript disabled, since the report HTML is static.
/// </summary>
/// <param name="options">Renderer configuration.</param>
public sealed class PlaywrightPdfRenderer(IOptions<PdfRendererOptions> options) : IPdfRenderer, IAsyncDisposable
{
    private readonly SemaphoreSlim startLock = new(1, 1);
    private IPlaywright? playwright;
    private IBrowser? browser;

    /// <inheritdoc />
    public async Task<byte[]> RenderAsync(string html, string footerText, CancellationToken cancellationToken)
    {
        var chromium = await GetBrowserAsync(cancellationToken);

        await using var context = await chromium.NewContextAsync(new BrowserNewContextOptions { JavaScriptEnabled = false });
        var page = await context.NewPageAsync();

        await page.SetContentAsync(html, new PageSetContentOptions { WaitUntil = WaitUntilState.Load });

        return await page.PdfAsync(new PagePdfOptions
        {
            PrintBackground = true,
            PreferCSSPageSize = true,
            Tagged = true,
            DisplayHeaderFooter = true,
            HeaderTemplate = "<span></span>",
            FooterTemplate =
                "<div style=\"width:100%;font-size:8px;font-family:Arial,sans-serif;text-align:center\">" +
                $"{WebUtility.HtmlEncode(footerText)} Page <span class=\"pageNumber\"></span> of <span class=\"totalPages\"></span></div>"
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

            var executablePath = options.Value.ChromiumExecutablePath;
            browser = await LaunchAsync(playwright, string.IsNullOrWhiteSpace(executablePath) ? null : executablePath);

            return browser;
        }
        finally
        {
            startLock.Release();
        }
    }

    // Playwright's own Chromium first; if it was never installed, use Edge or Chrome already on the machine.
    private static async Task<IBrowser> LaunchAsync(IPlaywright instance, string? executablePath)
    {
        if (executablePath is not null)
        {
            return await instance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { ExecutablePath = executablePath });
        }

        try
        {
            return await instance.Chromium.LaunchAsync();
        }
        catch (PlaywrightException)
        {
            try
            {
                return await instance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "msedge" });
            }
            catch (PlaywrightException)
            {
                return await instance.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Channel = "chrome" });
            }
        }
    }
}
