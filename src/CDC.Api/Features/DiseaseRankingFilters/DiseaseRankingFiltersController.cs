using CDC.Api.Application.Extensions;
using CDC.Api.Features.DiseaseRankingFilters.Commands;
using CDC.Api.Features.DiseaseRankingFilters.Dtos;
using CDC.Api.Features.DiseaseRankingFilters.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.DiseaseRankingFilters;

/// <summary>
/// Disease ranking filters: species and criteria groupings used to produce disease ranking
/// reports. Replaces the legacy <c>EditDiseaseRankingFilters.aspx</c> page.
/// </summary>
/// <param name="mediator">Dispatches queries and commands to their handlers.</param>
[ApiController]
[Route("api/disease-ranking-filters")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class DiseaseRankingFiltersController(ISender mediator) : ControllerBase
{
    /// <summary>Gets every active disease ranking filter.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every active filter.</returns>
    /// <response code="200">The filters were retrieved.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<DiseaseRankingFilterSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DiseaseRankingFilterSummaryDto>>> GetFilters(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetDiseaseRankingFiltersQuery(), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Gets one disease ranking filter's full detail.</summary>
    /// <param name="filterNumber">The filter to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The filter detail.</returns>
    /// <response code="200">The filter was retrieved.</response>
    /// <response code="404">No active filter exists with that number.</response>
    [HttpGet("{filterNumber:long}")]
    [ProducesResponseType(typeof(DiseaseRankingFilterDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DiseaseRankingFilterDetailDto>> GetFilterDetail(long filterNumber, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetDiseaseRankingFilterDetailQuery(filterNumber), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Creates a new disease ranking filter.</summary>
    /// <param name="request">The filter's name, criteria and selected species.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The saved filter.</returns>
    /// <response code="200">The filter was created.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="409">A filter with this name already exists.</response>
    [HttpPost]
    [ProducesResponseType(typeof(DiseaseRankingFilterDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DiseaseRankingFilterDetailDto>> CreateFilter(
        [FromBody] SaveDiseaseRankingFilterRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SaveDiseaseRankingFilterCommand(null, request), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Updates an existing disease ranking filter's criteria and species selection.</summary>
    /// <param name="filterNumber">The filter to update.</param>
    /// <param name="request">The filter's new name, criteria and selected species.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The saved filter.</returns>
    /// <response code="200">The filter was updated.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpPut("{filterNumber:long}")]
    [ProducesResponseType(typeof(DiseaseRankingFilterDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DiseaseRankingFilterDetailDto>> UpdateFilter(
        long filterNumber,
        [FromBody] SaveDiseaseRankingFilterRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SaveDiseaseRankingFilterCommand(filterNumber, request), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Deletes a disease ranking filter.</summary>
    /// <param name="filterNumber">The filter to delete.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The filter was deleted.</response>
    [HttpDelete("{filterNumber:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<ActionResult> DeleteFilter(long filterNumber, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteDiseaseRankingFilterCommand(filterNumber), cancellationToken);

        return result.ToNoContentActionResult(this);
    }
}
