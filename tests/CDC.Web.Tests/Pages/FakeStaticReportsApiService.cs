using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Pages;

// Test double for IStaticReportsApiService so page-model/integration tests don't need a real HTTP call.
internal sealed class FakeStaticReportsApiService(
    IReadOnlyList<StaticReportVersionDto>? currentReports = null,
    StaticReportDataDto? data = null,
    Exception? throwOnGetCurrent = null)
    : IStaticReportsApiService
{
    private readonly IReadOnlyList<StaticReportVersionDto> _currentReports = currentReports ?? [];

    public Task<IReadOnlyList<StaticReportVersionDto>> GetCurrentAsync(bool isUserManual, CancellationToken cancellationToken = default) =>
        throwOnGetCurrent is not null
            ? Task.FromException<IReadOnlyList<StaticReportVersionDto>>(throwOnGetCurrent)
            : Task.FromResult(_currentReports);

    public Task<StaticReportDataDto?> GetDataAsync(Guid staticReportVersionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(data);
}
