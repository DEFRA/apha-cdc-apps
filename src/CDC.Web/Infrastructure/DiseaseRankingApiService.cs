using System.Net;
using System.Net.Http.Json;
using CDC.Common.Contracts;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Default <see cref="IDiseaseRankingApiService"/>. The base address is configured on the
/// underlying <see cref="HttpClient"/> in Program.cs (from <c>Api:BaseUrl</c>). Failures propagate to
/// the caller, which decides how to present them.
/// </summary>
/// <param name="httpClient">Typed client pointing at CDC.Api.</param>
public sealed class DiseaseRankingApiService(HttpClient httpClient) : IDiseaseRankingApiService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<DiseaseRankingReportOptionDto>> GetReportOptionsAsync(CancellationToken cancellationToken = default)
    {
        var options = await httpClient.GetFromJsonAsync<IReadOnlyList<DiseaseRankingReportOptionDto>>(
            "/api/disease-ranking/report-options", cancellationToken);

        return options ?? [];
    }

    /// <inheritdoc />
    public async Task<DiseaseRankingReportDto?> GetReportAsync(
        string reportKey,
        DiseaseRankingReportKind kind,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"/api/disease-ranking/reports/{Uri.EscapeDataString(reportKey)}?kind={kind}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<DiseaseRankingReportDto>(cancellationToken);
    }
}
