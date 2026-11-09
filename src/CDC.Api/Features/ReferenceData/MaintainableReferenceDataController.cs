using CDC.Api.Application;
using CDC.Api.Application.Extensions;
using CDC.Api.Features.ReferenceData.Commands;
using CDC.Api.Features.ReferenceData.Dtos;
using CDC.Api.Features.ReferenceData.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.ReferenceData;

/// <summary>
/// The "maintain reference data" admin screen: the maintainable reference tables, their values, changes to them and their
/// audit trail. Replaces the legacy <c>IReferenceValueService</c> WCF endpoint. Profile editors only, as legacy
/// <c>ReferenceValueChange.CanAddReferenceValueChange</c>.
/// </summary>
/// <param name="mediator">Dispatches queries and commands to their handlers.</param>
/// <param name="userContext">The signed-in user, recorded against every change.</param>
[ApiController]
[Route("api/reference-data/maintainable-tables")]
[Produces("application/json")]
[RequireGlobalRole(GlobalRole.ProfileEditor)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class MaintainableReferenceDataController(ISender mediator, IUserContext userContext) : ControllerBase
{
    /// <summary>Gets the reference tables a profile editor can maintain.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The maintainable tables.</returns>
    /// <response code="200">The tables were retrieved.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MaintainableReferenceTableDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MaintainableReferenceTableDto>>> GetTables(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetMaintainableReferenceTablesQuery(), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Gets every value of one maintainable table, in sequence order.</summary>
    /// <param name="referenceTableId">The table to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The values.</returns>
    /// <response code="200">The values were retrieved.</response>
    /// <response code="404">The table does not exist or is not maintainable.</response>
    [HttpGet("{referenceTableId:guid}/values")]
    [ProducesResponseType(typeof(IReadOnlyList<MaintainableReferenceValueDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<MaintainableReferenceValueDto>>> GetValues(
        Guid referenceTableId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetMaintainableReferenceValuesQuery(referenceTableId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Gets one value of a maintainable table, with the <c>lastUpdated</c> row version needed to change it.</summary>
    /// <param name="referenceTableId">The table the value belongs to.</param>
    /// <param name="valueId">The value to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The value.</returns>
    /// <response code="200">The value was retrieved.</response>
    /// <response code="404">The value is not in that maintainable table.</response>
    [HttpGet("{referenceTableId:guid}/values/{valueId:guid}")]
    [ProducesResponseType(typeof(MaintainableReferenceValueDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintainableReferenceValueDto>> GetValue(
        Guid referenceTableId,
        Guid valueId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetMaintainableReferenceValueQuery(referenceTableId, valueId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Gets the audit trail of one maintainable table, most recent change first.</summary>
    /// <param name="referenceTableId">The table to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The audit entries.</returns>
    /// <response code="200">The audit trail was retrieved.</response>
    /// <response code="404">The table does not exist or is not maintainable.</response>
    [HttpGet("{referenceTableId:guid}/audit-trail")]
    [ProducesResponseType(typeof(IReadOnlyList<ReferenceTableAuditEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<ReferenceTableAuditEntryDto>>> GetAuditTrail(
        Guid referenceTableId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetReferenceTableAuditTrailQuery(referenceTableId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Edits, adds, inactivates or deletes one value, recording the change in the audit trail.</summary>
    /// <remarks>
    /// <c>action</c> is <c>0</c> edit, <c>1</c> add, <c>2</c> inactivate, <c>3</c> delete. Supply the <c>lastUpdated</c>
    /// row version from <c>GET .../values/{valueId}</c> unless adding; if another user has changed the value since, the
    /// request is rejected with 409 and nothing is written.
    ///
    /// Sample request:
    ///
    ///     POST /api/reference-data/maintainable-tables/{referenceTableId}/changes
    ///     {
    ///       "action": 0,
    ///       "valueId": "3f4a5b6c-7d8e-9f0a-1b2c-3d4e5f607182",
    ///       "newLookupValue": "Import controls",
    ///       "reason": "Corrected spelling",
    ///       "lastUpdated": "AAAAAAAAB9E="
    ///     }
    /// </remarks>
    /// <param name="referenceTableId">The table the value belongs to.</param>
    /// <param name="request">The change to apply.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The value changed, or the identifier given to an added value.</returns>
    /// <response code="200">The change was committed.</response>
    /// <response code="400">The request failed validation, or the table already has that value.</response>
    /// <response code="404">The table or value does not exist, or cannot be changed that way.</response>
    /// <response code="409">Another user has changed the value since it was read.</response>
    [HttpPost("{referenceTableId:guid}/changes")]
    [ProducesResponseType(typeof(ChangeReferenceValueResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ChangeReferenceValueResultDto>> ChangeValue(
        Guid referenceTableId,
        [FromBody] ChangeReferenceValueRequestDto request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // UserId is the signed-in user, never taken from the request body, so the audit trail cannot be spoofed.
        var command = new ChangeReferenceValueCommand
        {
            ReferenceTableId = referenceTableId,
            Action = request.Action,
            ValueId = request.ValueId,
            NewLookupValue = request.NewLookupValue,
            Reason = request.Reason,
            LastUpdated = request.LastUpdated,
            UserId = userContext.RequireUserId()
        };

        var result = await mediator.Send(command, cancellationToken);

        return result.ToActionResult(this);
    }
}
