using CDC.Api.Application.Extensions;
using CDC.Api.Features.ReferenceData.Dtos;
using CDC.Api.Features.ReferenceData.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.ReferenceData;

/// <summary>
/// Generic reference table values, used to resolve "List" type question field options (for
/// example the species questionnaire).
/// </summary>
/// <param name="mediator">Dispatches queries to their handlers.</param>
[ApiController]
[Route("api/reference-data")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class ReferenceDataController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Gets every value in one reference table.
    /// </summary>
    /// <remarks>
    /// Sample response:
    ///
    ///     [
    ///       { "id": "3f4a5b6c-7d8e-9f0a-1b2c-3d4e5f607182", "value": "Each individual &amp; its movement recorded nationally" }
    ///     ]
    /// </remarks>
    /// <param name="referenceTableId">The reference table to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The reference values; an empty list when the table has none.</returns>
    /// <response code="200">The reference values were retrieved.</response>
    [HttpGet("{referenceTableId:guid}/values")]
    [ProducesResponseType(typeof(IReadOnlyList<ReferenceValueDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ReferenceValueDto>>> GetReferenceValues(
        Guid referenceTableId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetReferenceValuesQuery(referenceTableId), cancellationToken);

        return result.ToActionResult(this);
    }
}
