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
    /// <returns><see langword="false"/> when no such version exists.</returns>
    Task<bool> DeleteStaticReportVersionAsync(Guid staticReportVersionId, CancellationToken cancellationToken);
}
