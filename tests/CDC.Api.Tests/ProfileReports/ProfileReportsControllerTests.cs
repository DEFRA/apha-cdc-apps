using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileReports;
using CDC.Api.Features.ProfileReports.Commands;
using CDC.Api.Features.ProfileReports.Dtos;
using CDC.Api.Features.ProfileReports.Queries;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Moq;

namespace CDC.Api.Tests.ProfileReports;

public class ProfileReportsControllerTests
{
    private readonly Mock<ISender> mediator = new(MockBehavior.Strict);

    private ProfileReportsController CreateController() => new(mediator.Object)
    {
        // ControllerBase.Problem() resolves this from the request services at runtime.
        ProblemDetailsFactory = new TestProblemDetailsFactory(),
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task GetProfileVersionReports_ReturnsOk()
    {
        IReadOnlyList<ProfileVersionReportDto> reports = [ProfileReportTestData.ProfileVersionReportDto()];

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetProfileVersionReportsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(reports));

        var response = await CreateController().GetProfileVersionReports(ProfileReportTestData.ProfileVersionId, false, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(reports);
    }

    [Fact]
    public async Task GetProfileVersionReports_ReturnsNotFound()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<GetProfileVersionReportsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<IReadOnlyList<ProfileVersionReportDto>>("not found"));

        var response = await CreateController().GetProfileVersionReports(ProfileReportTestData.ProfileVersionId, false, CancellationToken.None);

        AssertProblem(response.Result!, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetProfileReportData_ReturnsOk()
    {
        var dto = new ProfileReportDataDto { ReportData = ProfileReportTestData.ReportBytes };

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetProfileReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetProfileReportData(
            ProfileReportTestData.ProfileReportId,
            ProfileReportTestData.ProfileVersionId,
            CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfileReportData_ReturnsNotFound()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<GetProfileReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<ProfileReportDataDto>("not found"));

        var response = await CreateController().GetProfileReportData(
            ProfileReportTestData.ProfileReportId,
            ProfileReportTestData.ProfileVersionId,
            CancellationToken.None);

        AssertProblem(response.Result!, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task CreateProfileReport_ReturnsCreated()
    {
        var command = new CreateProfileReportCommand
        {
            ProfileVersionId = ProfileReportTestData.ProfileVersionId,
            ProfileReportId = ProfileReportTestData.ProfileReportId,
            ReportName = "FullProfileGUID",
            ReportData = ProfileReportTestData.ReportBytes
        };
        var resultDto = new CreateProfileReportResultDto { ProfileReportId = ProfileReportTestData.ProfileReportId };

        mediator
            .Setup(sender => sender.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(resultDto));

        var response = await CreateController().CreateProfileReport(command, CancellationToken.None);

        response.Result.Should().BeOfType<CreatedAtActionResult>().Which.Value.Should().BeSameAs(resultDto);
    }

    [Fact]
    public async Task GetContributionsReport_ReturnsOk()
    {
        var dto = new ContributionsReportDto { ProfileId = ProfileReportTestData.ProfileId };

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetContributionsReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetContributionsReport(ProfileReportTestData.ProfileId, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfilePrintVersion_ReturnsOk()
    {
        var dto = new ProfilePrintVersionDto { ProfileVersionId = ProfileReportTestData.ProfileVersionId };

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetProfilePrintVersionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetProfilePrintVersion(
            ProfileReportTestData.ProfileVersionId,
            ProfileReportTestData.ProfileSectionId,
            CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetComparisonReport_ReturnsOk()
    {
        var dto = new ProfileVersionComparisonReportDto();

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetProfileVersionComparisonReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetComparisonReport(
            ProfileReportTestData.ProfileVersionId,
            ProfileReportTestData.ProfileReportId,
            CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetBespokeReport_ReturnsOk()
    {
        var dto = new ProfileVersionBespokeReportDto();

        mediator
            .Setup(sender => sender.Send(
                It.Is<GetProfileVersionBespokeReportQuery>(q => q.ProfileVersionId == ProfileReportTestData.ProfileVersionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var query = new GetProfileVersionBespokeReportQuery(Guid.Empty, ["Section1"], [], [], "Template");
        var response = await CreateController().GetBespokeReport(ProfileReportTestData.ProfileVersionId, query, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetSummaryProfileReport_ReturnsOk()
    {
        var dto = new SummaryProfileReportDto();

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetSummaryProfileReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetSummaryProfileReport(ProfileReportTestData.ProfileVersionId, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetSummaryPrioritisationReport_ReturnsOk()
    {
        var dto = new SummaryPrioritisationReportDto();

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetSummaryPrioritisationReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetSummaryPrioritisationReport(ProfileReportTestData.ProfileVersionId, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetRankingReport_ReturnsOk()
    {
        var dto = new ProfileRankingReportDto { ReportType = ProfileRankingReportType.All };

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetProfileRankingReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetRankingReport(ProfileRankingReportType.All, "Filter", CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    private static ProblemDetails AssertProblem(IActionResult result, int expectedStatusCode)
    {
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(expectedStatusCode);

        return objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
    }

    /// <summary>Minimal factory so <c>ControllerBase.Problem()</c> works without the MVC pipeline.</summary>
    private sealed class TestProblemDetailsFactory : ProblemDetailsFactory
    {
        public override ProblemDetails CreateProblemDetails(
            HttpContext httpContext,
            int? statusCode = null,
            string? title = null,
            string? type = null,
            string? detail = null,
            string? instance = null) => new()
            {
                Status = statusCode ?? StatusCodes.Status500InternalServerError,
                Title = title,
                Type = type,
                Detail = detail,
                Instance = instance
            };

        public override ValidationProblemDetails CreateValidationProblemDetails(
            HttpContext httpContext,
            Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelStateDictionary,
            int? statusCode = null,
            string? title = null,
            string? type = null,
            string? detail = null,
            string? instance = null) => new(modelStateDictionary)
            {
                Status = statusCode ?? StatusCodes.Status400BadRequest,
                Title = title,
                Type = type,
                Detail = detail,
                Instance = instance
            };
    }
}
