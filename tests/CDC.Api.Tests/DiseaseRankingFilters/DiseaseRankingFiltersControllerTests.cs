using CDC.Api.Application.Extensions;
using CDC.Api.Domain.Common;
using CDC.Api.Features.DiseaseRankingFilters;
using CDC.Api.Features.DiseaseRankingFilters.Commands;
using CDC.Api.Features.DiseaseRankingFilters.Dtos;
using CDC.Api.Features.DiseaseRankingFilters.Queries;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Moq;

namespace CDC.Api.Tests.DiseaseRankingFilters;

public class DiseaseRankingFiltersControllerTests
{
    private static readonly Guid SpeciesId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly Mock<ISender> mediator = new(MockBehavior.Strict);

    private DiseaseRankingFiltersController CreateController() => new(mediator.Object)
    {
        // ControllerBase.Problem() resolves this from the request services at runtime.
        ProblemDetailsFactory = new TestProblemDetailsFactory(),
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    [Fact]
    public async Task GetFilters_ReturnsOk()
    {
        IReadOnlyList<DiseaseRankingFilterSummaryDto> filters = [new DiseaseRankingFilterSummaryDto { FilterNumber = 1, DiseaseName = "Anthrax" }];

        mediator
            .Setup(sender => sender.Send(It.IsAny<GetDiseaseRankingFiltersQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(filters));

        var response = await CreateController().GetFilters(CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.StatusCode.Should().Be(StatusCodes.Status200OK);
        ok.Value.Should().BeSameAs(filters);
    }

    [Fact]
    public async Task GetFilterDetail_ReturnsOk()
    {
        var detail = new DiseaseRankingFilterDetailDto { FilterNumber = 1, DiseaseName = "Anthrax", SpeciesIds = [SpeciesId] };

        mediator
            .Setup(sender => sender.Send(It.Is<GetDiseaseRankingFilterDetailQuery>(query => query.FilterNumber == 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(detail));

        var response = await CreateController().GetFilterDetail(1, CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(detail);
    }

    [Fact]
    public async Task GetFilterDetail_ReturnsNotFound_WhenNoActiveFilterExists()
    {
        mediator
            .Setup(sender => sender.Send(It.IsAny<GetDiseaseRankingFilterDetailQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<DiseaseRankingFilterDetailDto>("No active disease ranking filter exists with number '99'."));

        var response = await CreateController().GetFilterDetail(99, CancellationToken.None);

        var problem = AssertProblem(response.Result, StatusCodes.Status404NotFound);
        problem.Detail.Should().Be("No active disease ranking filter exists with number '99'.");
    }

    [Fact]
    public async Task CreateFilter_ReturnsOk()
    {
        var request = new SaveDiseaseRankingFilterRequestDto { DiseaseName = "Anthrax", SpeciesIds = [SpeciesId] };
        var saved = new DiseaseRankingFilterDetailDto { FilterNumber = 1, DiseaseName = "Anthrax", SpeciesIds = [SpeciesId] };

        mediator
            .Setup(sender => sender.Send(
                It.Is<SaveDiseaseRankingFilterCommand>(command => command.FilterNumber == null && command.Request == request),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(saved));

        var response = await CreateController().CreateFilter(request, CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(saved);
    }

    [Fact]
    public async Task CreateFilter_ReturnsConflict_WhenTheNameIsAlreadyInUse()
    {
        var request = new SaveDiseaseRankingFilterRequestDto { DiseaseName = "Anthrax", SpeciesIds = [SpeciesId] };

        mediator
            .Setup(sender => sender.Send(It.IsAny<SaveDiseaseRankingFilterCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<DiseaseRankingFilterDetailDto>("A disease ranking filter named 'Anthrax' already exists."));

        var response = await CreateController().CreateFilter(request, CancellationToken.None);

        var problem = AssertProblem(response.Result, StatusCodes.Status409Conflict);
        problem.Detail.Should().Be("A disease ranking filter named 'Anthrax' already exists.");
    }

    [Fact]
    public async Task UpdateFilter_ReturnsOk()
    {
        var request = new SaveDiseaseRankingFilterRequestDto { DiseaseName = "Anthrax", SpeciesIds = [SpeciesId] };
        var saved = new DiseaseRankingFilterDetailDto { FilterNumber = 1, DiseaseName = "Anthrax", SpeciesIds = [SpeciesId] };

        mediator
            .Setup(sender => sender.Send(
                It.Is<SaveDiseaseRankingFilterCommand>(command => command.FilterNumber == 1 && command.Request == request),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(saved));

        var response = await CreateController().UpdateFilter(1, request, CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(saved);
    }

    [Fact]
    public async Task UpdateFilter_ReturnsBadRequest_WhenValidationFails()
    {
        var request = new SaveDiseaseRankingFilterRequestDto();

        mediator
            .Setup(sender => sender.Send(It.IsAny<SaveDiseaseRankingFilterCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FluentValidation.ValidationException("Enter a report title."));

        var act = async () => await CreateController().UpdateFilter(1, request, CancellationToken.None);

        await act.Should().ThrowAsync<FluentValidation.ValidationException>();
    }

    [Fact]
    public async Task DeleteFilter_ReturnsNoContent()
    {
        mediator
            .Setup(sender => sender.Send(It.Is<DeleteDiseaseRankingFilterCommand>(command => command.FilterNumber == 1), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));

        var response = await CreateController().DeleteFilter(1, CancellationToken.None);

        response.Should().BeOfType<NoContentResult>();
    }

    private static ProblemDetails AssertProblem(ActionResult? result, int expectedStatusCode)
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
