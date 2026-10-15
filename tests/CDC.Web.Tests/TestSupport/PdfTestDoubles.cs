using CDC.Web.Infrastructure.Pdf;

namespace CDC.Web.Tests.TestSupport;

/// <summary>Stands in for the Razor view renderer: records what it was asked to render and returns canned HTML, or throws.</summary>
/// <param name="html">The HTML to return.</param>
/// <param name="throwOnRender">The exception to throw instead of returning the HTML.</param>
internal sealed class FakeViewRenderer(string html, Exception? throwOnRender = null) : IRazorViewRenderer
{
    /// <summary>Gets the view path most recently rendered.</summary>
    public string? ViewPath { get; private set; }

    /// <summary>Gets the model most recently rendered.</summary>
    public object? Model { get; private set; }

    public Task<string> RenderAsync<TModel>(string viewPath, TModel model)
    {
        ViewPath = viewPath;
        Model = model;

        return throwOnRender is not null ? throw throwOnRender : Task.FromResult(html);
    }
}

/// <summary>Stands in for the Chromium PDF step: records the HTML and options it was asked to print and returns canned bytes, or throws.</summary>
/// <param name="pdf">The PDF bytes to return.</param>
/// <param name="throwOnRender">The exception to throw instead of returning the PDF.</param>
internal sealed class FakePdfService(byte[] pdf, Exception? throwOnRender = null) : IPlaywrightPdfService
{
    /// <summary>Gets the HTML most recently printed.</summary>
    public string? Html { get; private set; }

    /// <summary>Gets the render options most recently used.</summary>
    public PdfRenderOptions? Options { get; private set; }

    public Task<byte[]> RenderAsync(string html, PdfRenderOptions options, CancellationToken cancellationToken = default)
    {
        Html = html;
        Options = options;

        return throwOnRender is not null ? throw throwOnRender : Task.FromResult(pdf);
    }
}
