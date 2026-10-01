using CDC.Api.Features.StaticReports.Dtos;
using CDC.Api.Features.StaticReports.Interfaces;

namespace CDC.Api.Features.StaticReports;

/// <summary>
/// Default <see cref="IStaticReportService"/>. Applies the ordering and delete rule the legacy
/// <c>StaticReports.aspx</c> page applied in its business layer.
/// </summary>
/// <param name="repository">Static report data access.</param>
/// <param name="logger">Structured logger.</param>
public sealed class StaticReportService(IStaticReportRepository repository, ILogger<StaticReportService> logger)
    : IStaticReportService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<StaticReportDto>> GetCurrentStaticReportsAsync(
        bool isUserManual,
        bool publicOnly,
        CancellationToken cancellationToken)
    {
        var reports = await repository.GetCurrentStaticReportsAsync(isUserManual, publicOnly, cancellationToken);

        logger.RetrievedCurrentStaticReports(reports.Count, isUserManual);

        // Legacy default sort for the list view is Title ascending.
        return [.. reports.OrderBy(report => report.Title, StringComparer.OrdinalIgnoreCase)];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<StaticReportDto>> GetStaticReportHistoryAsync(
        Guid staticReportId,
        bool publicOnly,
        CancellationToken cancellationToken)
    {
        var versions = await repository.GetStaticReportHistoryAsync(staticReportId, publicOnly, cancellationToken);

        logger.RetrievedStaticReportHistory(versions.Count, staticReportId);

        // Legacy default sort for the history view is VersionSortValue descending.
        return [.. versions.OrderByDescending(version => version.VersionMajor)];
    }

    /// <inheritdoc />
    public Task<StaticReportDataDto?> GetStaticReportDataAsync(Guid staticReportVersionId, CancellationToken cancellationToken) =>
        repository.GetStaticReportDataAsync(staticReportVersionId, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> DeleteStaticReportVersionAsync(Guid staticReportVersionId, CancellationToken cancellationToken)
    {
        var document = await repository.GetStaticReportDataAsync(staticReportVersionId, cancellationToken);

        if (document is null)
        {
            return false;
        }

        await repository.DeleteStaticReportVersionAsync(staticReportVersionId, cancellationToken);

        return true;
    }
}
