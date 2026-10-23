namespace CDC.Web.Infrastructure;

/// <summary>A static report PDF fetched from CDC.Api.</summary>
/// <param name="Content">The PDF bytes.</param>
/// <param name="ContentDisposition">The <c>Content-Disposition</c> header value CDC.Api sent, if any.</param>
public sealed record StaticReportDocument(byte[] Content, string? ContentDisposition);

/// <summary>
/// Fetches static report PDFs server-side so browsers never need a route to CDC.Api, which is only
/// reachable from inside the VPC.
/// </summary>
public interface IStaticReportDocumentApiService
{
    /// <summary>Gets the PDF for one static report version via
    /// <c>GET /api/static-reports/versions/{staticReportVersionId}/document</c>.</summary>
    /// <returns>The document, or <see langword="null"/> when no such version exists.</returns>
    Task<StaticReportDocument?> GetDocumentAsync(Guid staticReportVersionId, bool download, CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IStaticReportDocumentApiService"/>.</summary>
/// <param name="httpClient">Typed client pointing at CDC.Api.</param>
public sealed class StaticReportDocumentApiService(HttpClient httpClient) : IStaticReportDocumentApiService
{
    /// <inheritdoc />
    public async Task<StaticReportDocument?> GetDocumentAsync(Guid staticReportVersionId, bool download, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"/api/static-reports/versions/{staticReportVersionId}/document?download={(download ? "true" : "false")}",
            cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);

        return new StaticReportDocument(content, response.Content.Headers.ContentDisposition?.ToString());
    }
}
