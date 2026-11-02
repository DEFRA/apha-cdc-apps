using Microsoft.Playwright;

namespace CDC.Api.Infrastructure.Pdf;

/// <summary>Converts a self-contained HTML document to a landscape A4 PDF.</summary>
public interface IPdfRenderer
{
    /// <param name="html">A complete HTML document with inline styles; it must not need network access.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The PDF bytes.</returns>
    Task<byte[]> RenderLandscapeAsync(string html, CancellationToken cancellationToken = default);
}

/// <summary>
/// Renders PDFs with headless Chromium through Playwright. Scripting and network access are off because
/// the HTML includes stored profile content. <c>Pdf:ChromiumExecutablePath</c> selects an installed
/// Chromium-based browser instead of the one downloaded by <c>playwright install chromium</c>.
/// </summary>
public sealed class PlaywrightPdfRenderer(IConfiguration configuration) : IPdfRenderer
{
    /// <inheritdoc />
    public async Task<byte[]> RenderLandscapeAsync(string html, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(html);
        cancellationToken.ThrowIfCancellationRequested();

        // The job renders a handful of PDFs per run, so a browser per call is simpler than sharing one.
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true,
            ExecutablePath = configuration["Pdf:ChromiumExecutablePath"]
        });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions { JavaScriptEnabled = false });
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
            FooterTemplate = "<div style=\"width:100%;font-size:9px;font-family:Arial,sans-serif;text-align:center\">Page <span class=\"pageNumber\"></span> of <span class=\"totalPages\"></span></div>",
            Margin = new Margin { Top = "15mm", Bottom = "18mm", Left = "10mm", Right = "10mm" }
        });
    }
}
