namespace CDC.Web.Infrastructure.Pdf;

/// <summary>Renders HTML to PDF bytes with a headless browser.</summary>
public interface IPlaywrightPdfService
{
    /// <summary>Renders an HTML document to a PDF.</summary>
    /// <param name="html">The complete HTML document to print.</param>
    /// <param name="options">Page header, footer and margins.</param>
    /// <param name="cancellationToken">Cancels the render.</param>
    /// <returns>The PDF; always starts with the <c>%PDF</c> signature.</returns>
    /// <exception cref="PdfRenderException">Thrown when the document could not be rendered.</exception>
    Task<byte[]> RenderAsync(string html, PdfRenderOptions options, CancellationToken cancellationToken = default);
}

/// <summary>Page furniture and margins for a rendered PDF.</summary>
public sealed record PdfRenderOptions
{
    /// <summary>Gets the HTML repeated at the top of every page, or <see langword="null"/> for none. Chromium header-template rules apply.</summary>
    public string? HeaderHtml { get; init; }

    /// <summary>Gets the HTML repeated at the bottom of every page, or <see langword="null"/> for none. May use <c>pageNumber</c> and <c>totalPages</c> spans.</summary>
    public string? FooterHtml { get; init; }

    /// <summary>Gets the top page margin in millimetres; must leave room for the header.</summary>
    public double MarginTopMm { get; init; } = 15;

    /// <summary>Gets the bottom page margin in millimetres; must leave room for the footer.</summary>
    public double MarginBottomMm { get; init; } = 15;

    /// <summary>Gets the left page margin in millimetres.</summary>
    public double MarginLeftMm { get; init; } = 12.7;

    /// <summary>Gets the right page margin in millimetres.</summary>
    public double MarginRightMm { get; init; } = 12.7;

    /// <summary>Gets a value indicating whether the PDF gets a bookmark outline built from its headings.</summary>
    public bool GenerateOutline { get; init; }

    /// <summary>Gets a value indicating whether a CSS <c>@page</c> size (for example a landscape named page) takes priority over A4 portrait.</summary>
    public bool PreferCssPageSize { get; init; }
}

/// <summary>Thrown when a document could not be rendered to a valid PDF.</summary>
public sealed class PdfRenderException : Exception
{
    /// <summary>Initialises a new instance of the <see cref="PdfRenderException"/> class.</summary>
    public PdfRenderException()
    {
    }

    /// <summary>Initialises a new instance of the <see cref="PdfRenderException"/> class.</summary>
    /// <param name="message">What went wrong.</param>
    public PdfRenderException(string message)
        : base(message)
    {
    }

    /// <summary>Initialises a new instance of the <see cref="PdfRenderException"/> class.</summary>
    /// <param name="message">What went wrong.</param>
    /// <param name="innerException">The underlying failure.</param>
    public PdfRenderException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
