using System.Net;
using System.Net.Http.Json;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Default <see cref="IStaticReportsApiService"/>. The base address is configured on the
/// underlying <see cref="HttpClient"/> in Program.cs (from <c>Api:BaseUrl</c>).
/// </summary>
/// <param name="httpClient">Typed client pointing at CDC.Api.</param>
public sealed class StaticReportsApiService(HttpClient httpClient) : IStaticReportsApiService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<StaticReportVersionDto>> GetCurrentAsync(
        bool isUserManual,
        CancellationToken cancellationToken = default)
    {
        var versions = await httpClient.GetFromJsonAsync<IReadOnlyList<StaticReportVersionDto>>(
            $"/api/static-reports?isUserManual={isUserManual}", cancellationToken);

        return versions ?? [];
    }

    /// <inheritdoc />
    public async Task<StaticReportDataDto?> GetDataAsync(Guid staticReportVersionId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"/api/static-reports/{staticReportVersionId}/data", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<StaticReportDataDto>(cancellationToken);
    }
}
