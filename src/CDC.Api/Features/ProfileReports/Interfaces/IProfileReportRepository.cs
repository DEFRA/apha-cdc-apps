using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileReports.Commands;

namespace CDC.Api.Features.ProfileReports.Interfaces;

/// <summary>
/// Data access for profile reports. The two read-and-persist members map onto the stored
/// procedures the legacy <c>Profiles.DataAccess.Sql.ProfileReportService</c> used, so behaviour
/// is preserved. The other legacy operations never called a stored procedure at all - they
/// rendered PDFs in-process via TallPDF - so there is no repository member for them; see
/// <see cref="Domain.Entities.PendingReport"/>.
/// </summary>
public interface IProfileReportRepository
{
    /// <summary>Reads the reports available for a profile version via <c>spgProfileVersionReportByProfileVersionId</c>.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="isAuthenticated">Whether the caller is authenticated, matching the legacy visibility rule.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The available reports; empty when none are defined.</returns>
    Task<IReadOnlyList<ProfileVersionReport>> GetProfileVersionReportsAsync(
        Guid profileVersionId,
        bool isAuthenticated,
        CancellationToken cancellationToken);

    /// <summary>Reads a previously persisted report document via <c>spgProfileVersionReportData</c>.</summary>
    /// <param name="profileVersionId">The profile version the report covers.</param>
    /// <param name="profileReportId">The report definition to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The persisted document, or <see langword="null"/> when none has been generated.</returns>
    Task<ProfileReportData?> GetProfileReportDataAsync(Guid profileVersionId, Guid profileReportId, CancellationToken cancellationToken);

    /// <summary>Persists a generated report document via <c>spiProfileVersionReportData</c>.</summary>
    /// <param name="command">The report to persist.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The persisted report's identifier.</returns>
    Task<Guid> CreateProfileReportAsync(CreateProfileReportCommand command, CancellationToken cancellationToken);

    /// <summary>Reads a profile's draft versions via <c>spgProfileHistory</c>, for the contributions report.</summary>
    /// <param name="profileId">The profile to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The draft versions, newest first; empty when the profile has none.</returns>
    Task<IReadOnlyList<DraftProfileVersion>> GetDraftProfileVersionsAsync(Guid profileId, CancellationToken cancellationToken);

    /// <summary>Reads one profile version's title, number, state and dates via <c>spgProfileVersionInfoById</c>.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The version's header, or <see langword="null"/> when it does not exist.</returns>
    Task<ProfileVersionHeader?> GetProfileVersionHeaderAsync(Guid profileVersionId, CancellationToken cancellationToken);

    /// <summary>Reads the profile sections whose author, VA or policy review falls due in a date range for one user via <c>spgaDiseaseReviews</c>.</summary>
    /// <param name="fromDate">The first day of the range.</param>
    /// <param name="toDate">The last day of the range.</param>
    /// <param name="userId">The user the sections are assigned to.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>One row per section, ordered by profile then section number; empty when none is due.</returns>
    Task<IReadOnlyList<DiseaseReviewSection>> GetDiseaseReviewsAsync(DateTime fromDate, DateTime toDate, Guid userId, CancellationToken cancellationToken);
}
