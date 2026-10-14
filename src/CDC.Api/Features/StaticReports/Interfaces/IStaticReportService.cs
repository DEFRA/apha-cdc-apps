using CDC.Api.Features.StaticReports.Dtos;

namespace CDC.Api.Features.StaticReports.Interfaces;

/// <summary>
/// Reads and maintains static reports and user manuals. Mirrors the legacy
/// <c>ProfilesLibrary.StaticReportList</c> / <c>StaticReportData</c> business objects.
/// </summary>
public interface IStaticReportService
{
    /// <summary>Gets the current version of every static report or user manual, title order.</summary>
    Task<IReadOnlyList<StaticReportDto>> GetCurrentStaticReportsAsync(
        bool isUserManual,
        bool publicOnly,
        CancellationToken cancellationToken);

    /// <summary>Gets every version of one static report or user manual, newest version first.</summary>
    Task<IReadOnlyList<StaticReportDto>> GetStaticReportHistoryAsync(
        Guid staticReportId,
        bool publicOnly,
        CancellationToken cancellationToken);

    /// <summary>Gets the stored document for one version.</summary>
    /// <returns>The document, or <see langword="null"/> when no such version exists.</returns>
    Task<StaticReportDataDto?> GetStaticReportDataAsync(Guid staticReportVersionId, CancellationToken cancellationToken);

    /// <summary>Deletes one version.</summary>
    /// <returns>Whether the version was deleted, was not found, or the current user is not permitted to delete it.</returns>
    Task<DeleteStaticReportVersionOutcome> DeleteStaticReportVersionAsync(Guid staticReportVersionId, CancellationToken cancellationToken);

    /// <summary>Gets a value indicating whether the current user may upload static reports or user
    /// manuals. Mirrors the legacy <c>UploadStaticReportCommand.CanUploadStaticReport</c>.</summary>
    bool CanUploadStaticReports { get; }

    /// <summary>Uploads a new version of a static report or user manual.</summary>
    Task<UploadStaticReportResult> UploadStaticReportAsync(UploadStaticReportRequestDto request, CancellationToken cancellationToken);
}
