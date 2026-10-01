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
        // ControllerBase.Problem() resolves this from the request services at runtime.
        ProblemDetailsFactory = new TestProblemDetailsFactory(),
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task GetCategories_ReturnsOk()
    {
        IReadOnlyList<PrioritisationCategoryDto> categories = [new PrioritisationCategoryDto { Name = "Animal welfare" }];

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetPrioritisationCategoriesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(categories));

        var response = await CreateController().GetCategories(CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(categories);
    }

    [Fact]
    public async Task UpdateCriterion_ReturnsNoContent()
    {
        UpdateCriterionCommand? sentCommand = null;

        mediator
            .Setup(sender => sender.Send(It.IsAny<UpdateCriterionCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest<Result<Unit>>, CancellationToken>((command, _) => sentCommand = (UpdateCriterionCommand)command)
            .ReturnsAsync(Result.Success(Unit.Value));

        var request = new UpdateCriterionRequestDto
        {
            Weight = 42,
            ValueScores = [new UpdateCriterionValueScoreRequestDto { ValueId = ValueId, Score = 7 }]
        };

        var response = await CreateController().UpdateCriterion(CriterionId, request, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
        sentCommand!.ValueScores.Should().ContainSingle().Which.ValueId.Should().Be(ValueId);
    }


    [Fact]
    public async Task UpdateCriterion_ReturnsNotFound()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<UpdateCriterionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<Unit>("not found"));

        var request = new UpdateCriterionRequestDto { Weight = 42 };

        var response = await CreateController().UpdateCriterion(CriterionId, request, CancellationToken.None);

        var objectResult = response.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task GetRankingRange_ReturnsOk()
    {
        var dto = new PrioritisationRankingRangeDto { LowerBound = 10, UpperBound = 90 };

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetRankingRangeQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().GetRankingRange(CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task UpdateRankingRange_ReturnsOk()
    {
        var dto = new PrioritisationRankingRangeDto { LowerBound = 10, UpperBound = 90, RowVersion = "AQIDBAUGBwg=" };

        mediator
            .Setup(sender => sender.Send(It.IsAny<UpdateRankingRangeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(dto));

        var response = await CreateController().UpdateRankingRange(dto, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task UpdateRankingRange_ReturnsConflict()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<UpdateRankingRangeCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<PrioritisationRankingRangeDto>("conflict"));

        var dto = new PrioritisationRankingRangeDto { LowerBound = 10, UpperBound = 90, RowVersion = "AQIDBAUGBwg=" };

        var response = await CreateController().UpdateRankingRange(dto, CancellationToken.None);

        var objectResult = response.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status409Conflict);
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
