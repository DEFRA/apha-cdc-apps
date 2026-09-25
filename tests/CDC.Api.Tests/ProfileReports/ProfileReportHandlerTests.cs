using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileReports.Commands;
using CDC.Api.Features.ProfileReports.Dtos;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Api.Features.ProfileReports.Queries;
using FluentAssertions;
using Moq;

namespace CDC.Api.Tests.ProfileReports;

public class ProfileReportHandlerTests
{
    private readonly Mock<IProfileReportService> service = new(MockBehavior.Strict);

    [Fact]
    public async Task GetProfileVersionReportsQueryHandler_ReturnsSuccess()
    {
        IReadOnlyList<ProfileVersionReportDto> reports = [ProfileReportTestData.ProfileVersionReportDto()];

        service
            .Setup(svc => svc.GetProfileVersionReportsAsync(ProfileReportTestData.ProfileVersionId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reports);

        var result = await new GetProfileVersionReportsQueryHandler(service.Object)
            .Handle(new GetProfileVersionReportsQuery(ProfileReportTestData.ProfileVersionId, false), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(reports);
    }

    [Fact]
    public async Task GetProfileReportDataQueryHandler_ReturnsNotFound_WhenServiceReturnsNull()
    {
        service
            .Setup(svc => svc.GetProfileReportDataAsync(
                ProfileReportTestData.ProfileVersionId,
                ProfileReportTestData.ProfileReportId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileReportDataDto?)null);

        var result = await new GetProfileReportDataQueryHandler(service.Object).Handle(
            new GetProfileReportDataQuery(ProfileReportTestData.ProfileVersionId, ProfileReportTestData.ProfileReportId),
            CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetProfileReportDataQueryHandler_ReturnsSuccess()
    {
        var dto = new ProfileReportDataDto { ReportData = ProfileReportTestData.ReportBytes };

        service
            .Setup(svc => svc.GetProfileReportDataAsync(
                ProfileReportTestData.ProfileVersionId,
                ProfileReportTestData.ProfileReportId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await new GetProfileReportDataQueryHandler(service.Object).Handle(
            new GetProfileReportDataQuery(ProfileReportTestData.ProfileVersionId, ProfileReportTestData.ProfileReportId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task CreateProfileReportCommandHandler_ReturnsSuccess()
    {
        var command = new CreateProfileReportCommand
        {
            ProfileVersionId = ProfileReportTestData.ProfileVersionId,
            ProfileReportId = ProfileReportTestData.ProfileReportId,
            ReportName = "FullProfileGUID",
            ReportData = ProfileReportTestData.ReportBytes
        };
        var resultDto = new CreateProfileReportResultDto { ProfileReportId = ProfileReportTestData.ProfileReportId };

        service.Setup(svc => svc.CreateProfileReportAsync(command, It.IsAny<CancellationToken>())).ReturnsAsync(resultDto);

        var result = await new CreateProfileReportCommandHandler(service.Object).Handle(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(resultDto);
    }

    [Fact]
    public async Task GetContributionsReportQueryHandler_ReturnsSuccess()
    {
        var dto = new ContributionsReportDto { ProfileId = ProfileReportTestData.ProfileId, IsAvailable = false };

        service
            .Setup(svc => svc.GetContributionsReportAsync(ProfileReportTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await new GetContributionsReportQueryHandler(service.Object)
            .Handle(new GetContributionsReportQuery(ProfileReportTestData.ProfileId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfilePrintVersionQueryHandler_ReturnsSuccess()
    {
        var dto = new ProfilePrintVersionDto { ProfileVersionId = ProfileReportTestData.ProfileVersionId };

        service
            .Setup(svc => svc.GetProfilePrintVersionAsync(
                ProfileReportTestData.ProfileVersionId,
                ProfileReportTestData.ProfileSectionId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await new GetProfilePrintVersionQueryHandler(service.Object).Handle(
            new GetProfilePrintVersionQuery(ProfileReportTestData.ProfileVersionId, ProfileReportTestData.ProfileSectionId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfileVersionComparisonReportQueryHandler_ReturnsSuccess()
    {
        var dto = new ProfileVersionComparisonReportDto();

        service
            .Setup(svc => svc.GetProfileVersionComparisonReportAsync(
                ProfileReportTestData.ProfileVersionId,
                ProfileReportTestData.ProfileReportId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await new GetProfileVersionComparisonReportQueryHandler(service.Object).Handle(
            new GetProfileVersionComparisonReportQuery(ProfileReportTestData.ProfileVersionId, ProfileReportTestData.ProfileReportId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfileVersionBespokeReportQueryHandler_ReturnsSuccess()
    {
        var dto = new ProfileVersionBespokeReportDto();
        var query = new GetProfileVersionBespokeReportQuery(ProfileReportTestData.ProfileVersionId, ["Section1"], [], [], "Template");

        service
            .Setup(svc => svc.GetProfileVersionBespokeReportAsync(
                query.ProfileVersionId,
                query.SelectedSections,
                query.SelectedQuestions,
                query.SelectedGuidance,
                query.TemplateTitle,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await new GetProfileVersionBespokeReportQueryHandler(service.Object).Handle(query, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetSummaryPrioritisationReportQueryHandler_ReturnsSuccess()
    {
        var dto = new SummaryPrioritisationReportDto();

        service
            .Setup(svc => svc.GetSummaryPrioritisationReportAsync(ProfileReportTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await new GetSummaryPrioritisationReportQueryHandler(service.Object)
            .Handle(new GetSummaryPrioritisationReportQuery(ProfileReportTestData.ProfileVersionId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetSummaryProfileReportQueryHandler_ReturnsSuccess()
    {
        var dto = new SummaryProfileReportDto();

        service
            .Setup(svc => svc.GetSummaryProfileReportAsync(ProfileReportTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await new GetSummaryProfileReportQueryHandler(service.Object)
            .Handle(new GetSummaryProfileReportQuery(ProfileReportTestData.ProfileVersionId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfileRankingReportQueryHandler_ReturnsSuccess()
    {
        var dto = new ProfileRankingReportDto { ReportType = ProfileRankingReportType.All };

        service
            .Setup(svc => svc.GetProfileRankingReportAsync(ProfileRankingReportType.All, "Filter", It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await new GetProfileRankingReportQueryHandler(service.Object)
            .Handle(new GetProfileRankingReportQuery(ProfileRankingReportType.All, "Filter"), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(dto);
    }
}
