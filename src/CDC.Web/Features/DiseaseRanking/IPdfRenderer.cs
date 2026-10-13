namespace CDC.Web.Features.DiseaseRanking;

/// <summary>Turns HTML into a PDF document.</summary>
public interface IPdfRenderer
{
    /// <summary>Renders <paramref name="html"/> to a PDF.</summary>
    /// <param name="html">A complete HTML document. It must not depend on external resources.</param>
    /// <param name="footerText">Text shown in each page's footer, before the page number.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The PDF bytes.</returns>
    Task<byte[]> RenderAsync(string html, string footerText, CancellationToken cancellationToken);
}
