using CDC.Api.Application;
using CDC.Api.Features.StaticReports.Dtos;
using CDC.Api.Features.StaticReports.Interfaces;

namespace CDC.Api.Features.StaticReports;

/// <summary>
/// Default <see cref="IStaticReportService"/>. Applies the ordering and delete rule the legacy
/// <c>StaticReports.aspx</c> page applied in its business layer.
/// </summary>
/// <param name="repository">Static report data access.</param>
/// <param name="userContext">The current user's authoring role flags.</param>
/// <param name="logger">Structured logger.</param>
public sealed class StaticReportService(IStaticReportRepository repository, IUserContext userContext, ILogger<StaticReportService> logger)
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
        return [.. reports.OrderBy(report => report.Title, StringComparer.OrdinalIgnoreCase).Select(ApplyCanDelete)];
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
        return [.. versions.OrderByDescending(version => version.VersionMajor).Select(ApplyCanDelete)];
    }

    /// <inheritdoc />
    public Task<StaticReportDataDto?> GetStaticReportDataAsync(Guid staticReportVersionId, CancellationToken cancellationToken) =>
        repository.GetStaticReportDataAsync(staticReportVersionId, cancellationToken);

    /// <inheritdoc />
    public async Task<DeleteStaticReportVersionOutcome> DeleteStaticReportVersionAsync(Guid staticReportVersionId, CancellationToken cancellationToken)
    {
        var document = await repository.GetStaticReportDataAsync(staticReportVersionId, cancellationToken);

        if (document is null)
        {
            return DeleteStaticReportVersionOutcome.NotFound;
        }

        // Legacy StaticReport.CanDelete: IsCurrent AndAlso identity.IsProfileEditor. Current-ness
        // was already checked by the caller only offering the Delete link for the current row;
        // here only the permission half is enforced.
        if (!userContext.IsProfileEditor)
        {
            logger.DeleteStaticReportVersionForbidden(staticReportVersionId);
            return DeleteStaticReportVersionOutcome.Forbidden;
        }

        await repository.DeleteStaticReportVersionAsync(staticReportVersionId, cancellationToken);

        return DeleteStaticReportVersionOutcome.Success;
    }

    /// <inheritdoc />
    public bool CanUploadStaticReports => userContext.IsProfileEditor && !userContext.IsUserManagementSystem;

    /// <inheritdoc />
    public async Task<UploadStaticReportResult> UploadStaticReportAsync(UploadStaticReportRequestDto request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!CanUploadStaticReports)
        {
            logger.UploadStaticReportForbidden(request.Title);
            return new UploadStaticReportResult(UploadStaticReportOutcome.Forbidden, "You do not have permission to upload documents.");
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return new UploadStaticReportResult(UploadStaticReportOutcome.ValidationFailed, "Please choose a file to upload.");
        }

        if (request.PdfData.Length == 0)
        {
            return new UploadStaticReportResult(UploadStaticReportOutcome.ValidationFailed, "Please choose a file to upload.");
        }

        await repository.UploadStaticReportAsync(request.Title, request.PdfData, request.IsUserManual, request.IsPublic, cancellationToken);

        return new UploadStaticReportResult(UploadStaticReportOutcome.Success, null);
    }

    private StaticReportDto ApplyCanDelete(StaticReportDto report) =>
        report with { CanDelete = report.IsCurrent && userContext.IsProfileEditor };
}
