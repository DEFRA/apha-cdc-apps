using CDC.Web.Models;
using CDC.Web.Pages.HelpSupport;

namespace CDC.Web.Tests.Pages;

public class DocumentHistoryModelTests
{
    private static readonly Guid StaticReportId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task OnGetAsync_LoadsHistory_MostRecentFirst()
    {
        IReadOnlyList<StaticReportVersionDto> history =
        [
            new StaticReportVersionDto
            {
                Id = Guid.NewGuid(),
                StaticReportId = StaticReportId,
                Title = "Help using D2R2 guidance",
                VersionMajor = 0,
                EffectiveDateFrom = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
                IsCurrent = false
            },
            new StaticReportVersionDto
            {
                Id = Guid.NewGuid(),
                StaticReportId = StaticReportId,
                Title = "Help using D2R2 guidance",
                VersionMajor = 1,
                EffectiveDateFrom = new DateTime(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc),
                IsCurrent = true
            }
        ];
        var pageModel = new DocumentHistoryModel(
            new FakeStaticReportsApiService(history: history),
            new AlwaysEnabledLogger<DocumentHistoryModel>());

        await pageModel.OnGetAsync(StaticReportId, CancellationToken.None);

        Assert.False(pageModel.HasError);
        Assert.Equal(2, pageModel.Versions.Count);
        Assert.Equal("1.0", pageModel.Versions[0].Version);
        Assert.Equal("0.0", pageModel.Versions[1].Version);
    }

    [Fact]
    public async Task OnGetAsync_SetsHasError_WhenApiCallFails()
    {
        var pageModel = new DocumentHistoryModel(
            new FakeStaticReportsApiService(throwOnGetHistory: new HttpRequestException("connection refused")),
            new AlwaysEnabledLogger<DocumentHistoryModel>());

        await pageModel.OnGetAsync(StaticReportId, CancellationToken.None);

        Assert.True(pageModel.HasError);
        Assert.Empty(pageModel.Versions);
    }
}
