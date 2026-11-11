using CDC.Api.Application;
using CDC.Api.Application.Extensions;
using CDC.Api.Domain.Common;
using CDC.Api.Features.PrioritisationVariables.Commands;
using CDC.Api.Features.PrioritisationVariables.Dtos;
using CDC.Api.Features.PrioritisationVariables.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.PrioritisationVariables;

/// <summary>
/// Prioritisation categories and their criteria. Replaces the category/criteria portion of the
/// legacy <c>MaintainPrioritisationVariables.aspx</c> page.
/// </summary>
/// <param name="mediator">Dispatches queries to their handlers.</param>
[ApiController]
[Route("api/prioritisation-variables")]
[AuthorizeGlobalRole(GlobalRole.ProfileEditor)]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class PrioritisationVariablesController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Gets every prioritisation category with its criteria.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every category, each with its criteria.</returns>
    /// <response code="200">The categories were retrieved.</response>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(IReadOnlyList<PrioritisationCategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PrioritisationCategoryDto>>> GetCategories(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetPrioritisationCategoriesQuery(), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Updates a prioritisation criterion's weighting and all of its value scores.
    /// </summary>
    /// <param name="criterionId">The criterion to update.</param>
    /// <param name="request">The new weighting and value scores.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The criterion was updated.</response>
    /// <response code="404">No criterion exists with that id.</response>
    [HttpPut("criteria/{criterionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateCriterion(
        Guid criterionId,
        [FromBody] UpdateCriterionRequestDto request,
        CancellationToken cancellationToken)
    {
        var valueScores = request.ValueScores
            .Select(valueScore => new CriterionValueScore(valueScore.ValueId, valueScore.Score))
            .ToList();

        var result = await mediator.Send(new UpdateCriterionCommand(criterionId, request.Weight, valueScores, request.Name), cancellationToken);

        return result.ToNoContentActionResult(this);
    }

    /// <summary>
    /// Gets the ranking range profile prioritisation scores are normalised into.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The current ranking range and its row version.</returns>
    /// <response code="200">The ranking range was retrieved.</response>
    [HttpGet("ranking-range")]
    [ProducesResponseType(typeof(PrioritisationRankingRangeDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PrioritisationRankingRangeDto>> GetRankingRange(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetRankingRangeQuery(), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Updates the ranking range profile prioritisation scores are normalised into.
    /// </summary>
    /// <param name="request">The new bounds and the row version read alongside them.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The saved range with its new row version.</returns>
    /// <response code="200">The ranking range was updated.</response>
    /// <response code="409">Another user saved a change since the row version was read.</response>
    [HttpPut("ranking-range")]
    [ProducesResponseType(typeof(PrioritisationRankingRangeDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PrioritisationRankingRangeDto>> UpdateRankingRange(
        [FromBody] PrioritisationRankingRangeDto request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new UpdateRankingRangeCommand(request.LowerBound, request.UpperBound, request.RowVersion),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Calculates the prioritisation scores of every current draft profile version.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The draft profiles were prioritised.</response>
    [HttpPost("draft-prioritisation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> PrioritiseDraftProfiles(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new PrioritiseDraftProfilesCommand(), cancellationToken);

        return result.ToNoContentActionResult(this);
    }

    /// <summary>
    /// Recalculates the prioritisation scores of published profiles after the global prioritisation variables change.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The scores were recalculated.</response>
    [HttpPost("score-recalculation")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> RecalculateScores(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RecalculatePrioritisationScoresCommand(), cancellationToken);

        return result.ToNoContentActionResult(this);
    }

    /// <summary>
    /// Downloads the score data of one prioritisation category as CSV.
    /// </summary>
    /// <param name="categoryId">The category to export.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The CSV file.</response>
    /// <response code="404">No category exists with that id.</response>
    [HttpGet("categories/{categoryId:guid}/score-data")]
    [Produces("text/csv", "application/problem+json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> ExportCategoryScoreData(Guid categoryId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ExportCategoryScoreDataQuery(categoryId), cancellationToken);

        return ToCsvResult(result);
    }

    /// <summary>
    /// Downloads the normalised score and rank of every category, plus the combined rankings, as CSV.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The CSV file.</response>
    [HttpGet("normalised-scores")]
    [Produces("text/csv", "application/problem+json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<ActionResult> ExportNormalisedScores(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ExportNormalisedScoresQuery(), cancellationToken);

        return ToCsvResult(result);
    }

    private ActionResult ToCsvResult(Result<PrioritisationCsvExport> result) =>
        result.IsSuccess
            ? File(result.Value.Content, "text/csv", result.Value.FileName)
            : result.ToActionResult(this).Result!;
}
