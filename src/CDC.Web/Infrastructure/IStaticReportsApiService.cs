using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Typed client for the static report endpoints on CDC.Api: general reports and user manuals.
/// </summary>
public interface IStaticReportsApiService
{
    /// <summary>Calls <c>GET /api/static-reports</c> and returns the current version of every matching report.</summary>
    /// <param name="isUserManual">Whether to retrieve user manuals rather than general reports.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The current version of every matching report, empty when none are defined.</returns>
    Task<IReadOnlyList<StaticReportVersionDto>> GetCurrentAsync(bool isUserManual, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/static-reports/{staticReportId}/history</c>.</summary>
    /// <param name="staticReportId">The report whose history is being read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every version of the report, most recent first.</returns>
    Task<IReadOnlyList<StaticReportVersionDto>> GetHistoryAsync(Guid staticReportId, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/static-reports/{staticReportVersionId}/data</c>.</summary>
    /// <param name="staticReportVersionId">The version to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The PDF content, or <see langword="null"/> when no such version exists.</returns>
    Task<StaticReportDataDto?> GetDataAsync(Guid staticReportVersionId, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>POST /api/static-reports</c> to upload a new version.</summary>
    /// <param name="request">The title, PDF bytes and visibility for the new version.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome of the call.</returns>
    Task<StaticReportUpdateResult> UploadAsync(UploadStaticReportRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>DELETE /api/static-reports/{staticReportVersionId}</c>.</summary>
    /// <param name="staticReportVersionId">The version to delete.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome of the call.</returns>
    Task<StaticReportUpdateResult> DeleteAsync(Guid staticReportVersionId, CancellationToken cancellationToken = default);
}
