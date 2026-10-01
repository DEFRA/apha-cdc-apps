using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Typed client for the static reports endpoints on CDC.Api.
/// </summary>
public interface IStaticReportsApiService
{
    /// <summary>Calls <c>GET /api/static-reports</c> and returns the current version of every matching report.</summary>
    /// <param name="isUserManual">Whether to retrieve user manuals rather than general reports.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The current version of every matching report.</returns>
    Task<IReadOnlyList<StaticReportVersionDto>> GetCurrentAsync(bool isUserManual, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/static-reports/{staticReportVersionId}/data</c>.</summary>
    /// <param name="staticReportVersionId">The version to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The PDF content, or <see langword="null"/> when no such version exists.</returns>
    Task<StaticReportDataDto?> GetDataAsync(Guid staticReportVersionId, CancellationToken cancellationToken = default);
}
