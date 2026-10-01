using CDC.Api.Domain.Common;
using CDC.Api.Features.StaticReports;
using CDC.Api.Features.StaticReports.Commands;
using CDC.Api.Features.StaticReports.Dtos;
using CDC.Api.Features.StaticReports.Queries;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Moq;

namespace CDC.Api.Tests.StaticReports;

public class StaticReportsControllerTests
{
    private static readonly Guid VersionId = Guid.Parse("c80b8e93-21d2-453a-b0e0-3f522d03971d");

    private readonly Mock<ISender> mediator = new(MockBehavior.Strict);

    private StaticReportsController CreateController() => new(mediator.Object)
    {
        // ControllerBase.Problem() resolves this from the request services at runtime.
        ProblemDetailsFactory = new TestProblemDetailsFactory(),
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task GetCurrent_ReturnsOk()
    {
        IReadOnlyList<StaticReportVersionDto> reports = [new StaticReportVersionDto { Title = "D2R2 Quality Statement" }];

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetCurrentStaticReportsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(reports));

        var response = await CreateController().GetCurrent(isUserManual: true, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(reports);
    }

    [Fact]
    public async Task GetData_ReturnsOk()
    {
        var dto = new StaticReportDataDto { PdfData = [1, 2, 3], Title = "D2R2 Quality Statement" };

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetStaticReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetData(VersionId, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetData_ReturnsNotFound()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<GetStaticReportDataQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<StaticReportDataDto>("not found"));

        var response = await CreateController().GetData(VersionId, CancellationToken.None);

        AssertProblem(response.Result!, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task Upload_ReturnsNoContent()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<UploadStaticReportCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));

        var request = new UploadStaticReportRequestDto { Title = "D2R2 Quality Statement", PdfData = [1, 2, 3], IsUserManual = true, IsPublic = false };

        var response = await CreateController().Upload(request, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
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
