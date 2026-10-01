using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Pages;

// Test double for IStaticReportsApiService so page-model tests don't need a real HTTP call.
internal sealed class FakeStaticReportsApiService(
    IReadOnlyList<StaticReportVersionDto>? currentReports = null,
    IReadOnlyList<StaticReportVersionDto>? history = null,
    StaticReportDataDto? data = null,
    StaticReportUpdateResult? uploadResult = null,
    StaticReportUpdateResult? deleteResult = null,
    Exception? throwOnGetCurrent = null,
    Exception? throwOnGetHistory = null,
    Exception? throwOnGetData = null,
    Exception? throwOnUpload = null,
    Exception? throwOnDelete = null)
    : IStaticReportsApiService
{
    private readonly IReadOnlyList<StaticReportVersionDto> _currentReports = currentReports ?? [];
    private readonly IReadOnlyList<StaticReportVersionDto> _history = history ?? [];

    public Guid? DeletedStaticReportVersionId { get; private set; }

    public UploadStaticReportRequestDto? UploadedRequest { get; private set; }

    public Task<IReadOnlyList<StaticReportVersionDto>> GetCurrentAsync(bool isUserManual, CancellationToken cancellationToken = default) =>
        throwOnGetCurrent is not null
            ? Task.FromException<IReadOnlyList<StaticReportVersionDto>>(throwOnGetCurrent)
            : Task.FromResult(_currentReports);

    public Task<IReadOnlyList<StaticReportVersionDto>> GetHistoryAsync(Guid staticReportId, CancellationToken cancellationToken = default) =>
        throwOnGetHistory is not null
            ? Task.FromException<IReadOnlyList<StaticReportVersionDto>>(throwOnGetHistory)
            : Task.FromResult(_history);

    public Task<StaticReportDataDto?> GetDataAsync(Guid staticReportVersionId, CancellationToken cancellationToken = default) =>
        throwOnGetData is not null
            ? Task.FromException<StaticReportDataDto?>(throwOnGetData)
            : Task.FromResult(data);

    public Task<StaticReportUpdateResult> UploadAsync(UploadStaticReportRequestDto request, CancellationToken cancellationToken = default)
    {
        if (throwOnUpload is not null)
        {
            return Task.FromException<StaticReportUpdateResult>(throwOnUpload);
        }

        UploadedRequest = request;
        return Task.FromResult(uploadResult ?? new StaticReportUpdateResult { Outcome = StaticReportUpdateOutcome.Success });
    }

    public Task<StaticReportUpdateResult> DeleteAsync(Guid staticReportVersionId, CancellationToken cancellationToken = default)
    {
        if (throwOnDelete is not null)
        {
            return Task.FromException<StaticReportUpdateResult>(throwOnDelete);
        }

        DeletedStaticReportVersionId = staticReportVersionId;
        return Task.FromResult(deleteResult ?? new StaticReportUpdateResult { Outcome = StaticReportUpdateOutcome.Success });
    }
}
