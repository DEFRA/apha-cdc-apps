using CDC.Api.Application;
using CDC.Api.Application.Extensions;
using CDC.Api.Domain.Common;
using CDC.Api.Features.CrossCuttingIssueScores.Commands;
using CDC.Api.Features.CrossCuttingIssueScores.Dtos;
using CDC.Api.Features.CrossCuttingIssueScores.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.CrossCuttingIssueScores;

/// <summary>
/// Cross-cutting issue scores. Replaces the legacy <c>MaintainCrossCuttingIssueScores.aspx</c> page.
/// </summary>
/// <param name="mediator">Dispatches queries and commands to their handlers.</param>
[ApiController]
[Route("api/cross-cutting-issue-scores")]
[AuthorizeGlobalRole(GlobalRole.ProfileEditor)]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class CrossCuttingIssueScoresController(ISender mediator) : ControllerBase
{
    /// <summary>Gets every cross-cutting issue with its criteria and scored values.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The issues were retrieved.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CrossCuttingIssueCategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CrossCuttingIssueCategoryDto>>> GetCategories(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCrossCuttingIssueScoresQuery(), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Saves changed criterion-value scores, then recalculates species prioritisation scores.</summary>
    /// <param name="request">The changed scores.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The scores were saved.</response>
    /// <response code="400">A score is outside 0 to 100.</response>
    /// <response code="404">A criterion value does not exist.</response>
    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> UpdateScores(
        [FromBody] UpdateCrossCuttingIssueScoresRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateCrossCuttingIssueScoresCommand(request.Scores), cancellationToken);

        return result.ToNoContentActionResult(this);
    }
}
