using CDC.Api.Domain.Entities;

namespace CDC.Api.Features.StaticReports.Interfaces;

/// <summary>
/// Data access for static reports (general reports and user manuals). Implementations must
/// contain no business logic.
/// </summary>
public interface IStaticReportRepository
{
    /// <summary>Reads the current version of every report via <c>spgaCurrentStaticReport</c>.</summary>
    /// <param name="isUserManual">Whether to read user manuals rather than general reports.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The current version of every matching report.</returns>
    Task<IReadOnlyList<StaticReportVersion>> GetCurrentAsync(bool isUserManual, CancellationToken cancellationToken);

    /// <summary>Reads one version's PDF content via <c>spgStaticReportVersionData</c>.</summary>
    /// <param name="staticReportVersionId">The version to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The PDF content, or <see langword="null"/> when no such version exists.</returns>
    Task<StaticReportData?> GetDataAsync(Guid staticReportVersionId, CancellationToken cancellationToken);

    /// <summary>
    /// Uploads a new version via <c>spiStaticReport</c>. Supersedes the previous current version
    /// for the same title, if one exists.
    /// </summary>
    /// <param name="title">The report title. An existing report with this title gains a new version.</param>
    /// <param name="pdfData">The PDF bytes to store.</param>
    /// <param name="isUserManual">Whether this is a user manual rather than a general report.</param>
    /// <param name="isPublic">Whether this version is visible to unauthenticated users.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    Task UploadAsync(string title, byte[] pdfData, bool isUserManual, bool isPublic, CancellationToken cancellationToken);
}
