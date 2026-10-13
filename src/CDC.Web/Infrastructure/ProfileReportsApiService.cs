using System.Net;
using System.Net.Http.Json;
using CDC.Common.Contracts;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>Default <see cref="IProfileReportsApiService"/>.</summary>
/// <param name="httpClient">The CDC.Api client.</param>
public sealed class ProfileReportsApiService(HttpClient httpClient) : IProfileReportsApiService
{
    /// <inheritdoc />
    public async Task<ContributionsReportResult> GetContributionsReportAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"/api/profile-reports/contributions/{profileId}", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var report = await response.Content.ReadFromJsonAsync<ContributionsReportDto>(cancellationToken);

            return report is null
                ? new ContributionsReportResult(ContributionsReportOutcome.Error)
                : new ContributionsReportResult(ContributionsReportOutcome.Success, report);
        }

        return response.StatusCode switch
        {
            HttpStatusCode.NotFound => new ContributionsReportResult(ContributionsReportOutcome.NotFound),
            HttpStatusCode.Forbidden => new ContributionsReportResult(ContributionsReportOutcome.Forbidden),
            _ => new ContributionsReportResult(ContributionsReportOutcome.Error)
        };
    }
}
