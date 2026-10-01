using CDC.Api.Domain.Common;
using CDC.Api.Features.PrioritisationVariables;
using CDC.Api.Features.PrioritisationVariables.Commands;
using CDC.Api.Features.PrioritisationVariables.Dtos;
using CDC.Api.Features.PrioritisationVariables.Queries;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Moq;

namespace CDC.Api.Tests.PrioritisationVariables;

public class PrioritisationVariablesControllerTests
{
    private static readonly Guid CriterionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ValueId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly Mock<ISender> mediator = new(MockBehavior.Strict);

    private PrioritisationVariablesController CreateController() => new(mediator.Object)
    {
        ProblemDetailsFactory = new TestProblemDetailsFactory(),
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task GetCategories_ReturnsOk()
    {
        IReadOnlyList<PrioritisationCategoryDto> categories = [new PrioritisationCategoryDto { Id = Guid.NewGuid(), Name = "Animal welfare" }];

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetPrioritisationCategoriesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(categories));

        var response = await CreateController().GetCategories(CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(categories);
    }

    [Fact]
    public async Task UpdateCriterion_ReturnsNoContent()
    {
        var request = new UpdateCriterionRequestDto
        {
            Weight = 42,
            ValueScores = [new UpdateCriterionValueScoreRequestDto { ValueId = ValueId, Score = 99 }]
        };

        mediator
            .Setup(sender => sender.Send(
                It.Is<UpdateCriterionCommand>(command =>
                    command.CriterionId == CriterionId &&
                    command.Weight == 42 &&
                    command.ValueScores.Count == 1 &&
                    command.ValueScores[0].ValueId == ValueId &&
                    command.ValueScores[0].Score == 99),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));

        var response = await CreateController().UpdateCriterion(CriterionId, request, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task UpdateCriterion_ReturnsNotFound_WhenCriterionMissing()
    {
        var request = new UpdateCriterionRequestDto { Weight = 42 };

        mediator
            .Setup(sender => sender.Send(It.IsAny<UpdateCriterionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<Unit>("not found"));

        var response = await CreateController().UpdateCriterion(CriterionId, request, CancellationToken.None);

        AssertProblem(response, StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetRankingRange_ReturnsOk()
    {
        var dto = new PrioritisationRankingRangeDto { LowerBound = 15, UpperBound = 35, RowVersion = "AQIDBAUGBwg=" };

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetRankingRangeQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetRankingRange(CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task UpdateRankingRange_ReturnsOk()
    {
        var request = new PrioritisationRankingRangeDto { LowerBound = 15, UpperBound = 35, RowVersion = "AQIDBAUGBwg=" };
        var updated = request with { RowVersion = "CQoLDA0ODxA=" };

        mediator
            .Setup(sender => sender.Send(
                It.Is<UpdateRankingRangeCommand>(command =>
                    command.LowerBound == 15 && command.UpperBound == 35 && command.RowVersion == "AQIDBAUGBwg="),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(updated));

        var response = await CreateController().UpdateRankingRange(request, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(updated);
    }

    [Fact]
    public async Task UpdateRankingRange_ReturnsConflict_WhenRowVersionIsStale()
    {
        var request = new PrioritisationRankingRangeDto { LowerBound = 15, UpperBound = 35, RowVersion = "AQIDBAUGBwg=" };

        mediator
            .Setup(sender => sender.Send(It.IsAny<UpdateRankingRangeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<PrioritisationRankingRangeDto>("edited by another user"));

        var response = await CreateController().UpdateRankingRange(request, CancellationToken.None);

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
