using CDC.Api.Features.DiseaseRanking;
using CDC.Api.Features.DiseaseRanking.Interfaces;
using CDC.Common.Contracts;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CDC.Api.Tests.DiseaseRanking;

public sealed class DiseaseRankingControllerTests
{
    private readonly Mock<IDiseaseRankingService> service = new(MockBehavior.Strict);

    private DiseaseRankingController CreateController() => new(service.Object);

    [Fact]
    public async Task GetReportOptions_ReturnsOk()
    {
        IReadOnlyList<DiseaseRankingReportOptionDto> options = [new DiseaseRankingReportOptionDto { Key = "terrestrial", Label = "Terrestrial" }];
        service.Setup(s => s.GetReportOptionsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(options);

        var response = await CreateController().GetReportOptions(CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(options);
    }

    [Fact]
    public async Task GetReport_ReturnsOk_WhenTheReportExists()
    {
        var report = new DiseaseRankingReportDto { Title = "Report" };
        service.Setup(s => s.GetReportAsync("terrestrial", DiseaseRankingReportKind.Ranked, It.IsAny<CancellationToken>())).ReturnsAsync(report);

        var response = await CreateController().GetReport("terrestrial", DiseaseRankingReportKind.Ranked, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(report);
    }

    [Fact]
    public async Task GetReport_ReturnsNotFound_WhenTheReportDoesNotExist()
    {
        service.Setup(s => s.GetReportAsync("bogus", DiseaseRankingReportKind.Scored, It.IsAny<CancellationToken>())).ReturnsAsync((DiseaseRankingReportDto?)null);

        var response = await CreateController().GetReport("bogus", DiseaseRankingReportKind.Scored, CancellationToken.None);

        response.Result.Should().BeOfType<NotFoundResult>();
    }
}
