using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileQuestions;
using CDC.Api.Features.ProfileQuestions.Commands;
using CDC.Api.Features.ProfileQuestions.Dtos;
using CDC.Api.Features.ProfileQuestions.Queries;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Moq;

namespace CDC.Api.Tests.ProfileQuestions;

public class ProfileQuestionsControllerTests
{
    private readonly Mock<ISender> mediator = new(MockBehavior.Strict);

    private ProfileQuestionsController CreateController() => new(mediator.Object)
    {
        // ControllerBase.Problem() resolves this from the request services at runtime.
        ProblemDetailsFactory = new TestProblemDetailsFactory(),
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task GetProfileQuestion_ReturnsOk()
    {
        var dto = ProfileQuestionTestData.ProfileQuestionDto();

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetProfileQuestionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetProfileQuestion(ProfileQuestionTestData.QuestionId, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfileQuestion_ReturnsNotFound()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<GetProfileQuestionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<ProfileQuestionDto>("not found"));

        var response = await CreateController().GetProfileQuestion(ProfileQuestionTestData.QuestionId, CancellationToken.None);

        AssertProblem(response.Result!, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetProfileQuestionInfoList_ReturnsOk()
    {
        IReadOnlyList<ProfileQuestionInfoDto> questions = [new ProfileQuestionInfoDto { Id = ProfileQuestionTestData.QuestionId }];

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetProfileQuestionInfoListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(questions));

        var response = await CreateController().GetProfileQuestionInfoList(ProfileQuestionTestData.ProfileSectionId, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(questions);
    }

    [Fact]
    public async Task GetProfileGuidanceReport_ReturnsOk()
    {
        var dto = new ProfileGuidanceReportDto { ReportType = ProfileGuidanceReportType.All };

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetProfileGuidanceReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetProfileGuidanceReport(ProfileGuidanceReportType.All, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetProfileGuidanceReport_ReturnsNotFound()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<GetProfileGuidanceReportQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<ProfileGuidanceReportDto>("not found"));

        var response = await CreateController().GetProfileGuidanceReport(ProfileGuidanceReportType.All, CancellationToken.None);

        AssertProblem(response.Result!, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdateProfileQuestion_ReturnsOk()
    {
        var command = new UpdateProfileQuestionCommand { LastUpdated = ProfileQuestionTestData.RowVersion };
        var dto = ProfileQuestionTestData.ProfileQuestionDto();

        mediator
            .Setup(sender => sender.Send(
                It.Is<UpdateProfileQuestionCommand>(c => c.Id == ProfileQuestionTestData.QuestionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().UpdateProfileQuestion(ProfileQuestionTestData.QuestionId, command, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task UpdateProfileQuestion_ReturnsBadRequest()
    {
        // 400 is produced by the FluentValidation pipeline before the handler runs (see
        // ProfileQuestionValidatorTests), so at the controller level this is exercised via a
        // NotFound-shaped Result standing in for any non-success mapping through ToActionResult.
        var command = new UpdateProfileQuestionCommand { LastUpdated = ProfileQuestionTestData.RowVersion };

        mediator
            .Setup(sender => sender.Send(
                It.Is<UpdateProfileQuestionCommand>(c => c.Id == ProfileQuestionTestData.QuestionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<ProfileQuestionDto>("not found"));

        var response = await CreateController().UpdateProfileQuestion(ProfileQuestionTestData.QuestionId, command, CancellationToken.None);

        AssertProblem(response.Result!, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task UpdateProfileQuestion_ReturnsConflict()
    {
        var command = new UpdateProfileQuestionCommand { LastUpdated = ProfileQuestionTestData.RowVersion };

        mediator
            .Setup(sender => sender.Send(
                It.Is<UpdateProfileQuestionCommand>(c => c.Id == ProfileQuestionTestData.QuestionId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<ProfileQuestionDto>("edited by another user"));

        var response = await CreateController().UpdateProfileQuestion(ProfileQuestionTestData.QuestionId, command, CancellationToken.None);

        AssertProblem(response.Result!, StatusCodes.Status409Conflict);
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
