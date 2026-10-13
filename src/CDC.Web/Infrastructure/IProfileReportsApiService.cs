using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>Typed client for the profile report endpoints on CDC.Api.</summary>
public interface IProfileReportsApiService
{
    /// <summary>Calls <c>GET /api/profile-reports/contributions/{profileId}</c>.</summary>
    /// <param name="profileId">The profile to report on.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The report data, or why it is not available.</returns>
    Task<ContributionsReportResult> GetContributionsReportAsync(Guid profileId, CancellationToken cancellationToken = default);
}
