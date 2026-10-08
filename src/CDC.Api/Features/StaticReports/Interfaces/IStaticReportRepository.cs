using CDC.Api.Features.StaticReports.Dtos;

namespace CDC.Api.Features.StaticReports.Interfaces;

/// <summary>
/// Data access for static reports and user manuals. Mirrors the legacy
/// <c>Profiles.DataAccess.Sql.StaticReportService</c>.
/// </summary>
public interface IStaticReportRepository
{
    /// <summary>Reads the current version of every static report or user manual.</summary>
    /// <param name="isUserManual">Read user manuals rather than general reports.</param>
    /// <param name="publicOnly">Restrict to versions marked public, for unauthenticated callers.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<StaticReportDto>> GetCurrentStaticReportsAsync(
        bool isUserManual,
        bool publicOnly,
        CancellationToken cancellationToken);

    /// <summary>Reads every version of one static report or user manual.</summary>
    /// <param name="staticReportId">The logical report to read the history of.</param>
    /// <param name="publicOnly">Restrict to versions marked public, for unauthenticated callers.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<StaticReportDto>> GetStaticReportHistoryAsync(
        Guid staticReportId,
        bool publicOnly,
        CancellationToken cancellationToken);

    /// <summary>Reads the stored document for one version.</summary>
    /// <returns>The document, or <see langword="null"/> when no such version exists.</returns>
    Task<StaticReportDataDto?> GetStaticReportDataAsync(Guid staticReportVersionId, CancellationToken cancellationToken);

    /// <summary>Deletes one version.</summary>
    Task DeleteStaticReportVersionAsync(Guid staticReportVersionId, CancellationToken cancellationToken);
}
