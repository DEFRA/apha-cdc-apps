using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileSearch;
using CDC.Api.Features.ProfileSearch.Dtos;
using CDC.Api.Features.ProfileSearch.Queries;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Moq;

namespace CDC.Api.Tests.ProfileSearch;

public class ProfileControllerTests
{
    private readonly Mock<ISender> mediator = new(MockBehavior.Strict);

    private ProfileController CreateController() => new(mediator.Object)
    {
        ProblemDetailsFactory = new TestProblemDetailsFactory(),
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task GetAllProfiles_ReturnsOk()
    {
        IReadOnlyList<ProfileDto> profiles =
        [
            new() { Id = Guid.NewGuid(), Name = "Bovine tuberculosis", Status = "Published", IsActive = true }
        ];

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetAllProfilesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(profiles));

        var response = await CreateController().GetAllProfiles(CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        ok.Value.Should().BeSameAs(profiles);
    }

    [Fact]
    public async Task GetProfileVersion_ReturnsNotFound_WhenMissing()
    {
        var profileVersionId = Guid.NewGuid();

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetProfileVersionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<ProfileVersionDto>($"Profile version '{profileVersionId}' was not found."));

        var response = await CreateController().GetProfileVersion(profileVersionId, CancellationToken.None);

        var problem = AssertProblem(response.Result, StatusCodes.Status404NotFound);
        problem.Detail.Should().Be($"Profile version '{profileVersionId}' was not found.");
    }

    private static ProblemDetails AssertProblem(ActionResult? result, int expectedStatusCode)
    {
        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(expectedStatusCode);

        return objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
    }

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
