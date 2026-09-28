using CDC.Api.Application.Extensions;
using CDC.Api.Features.ProfileNotes.Commands;
using CDC.Api.Features.ProfileNotes.Dtos;
using CDC.Api.Features.ProfileNotes.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.ProfileNotes;

/// <summary>
/// Profile notes: comments and review points recorded against a profile version, optionally
/// linked to specific questions. Replaces the legacy <c>IProfileNoteService</c> WCF endpoint.
/// </summary>
/// <param name="mediator">Dispatches queries and commands to their handlers.</param>
[ApiController]
[Route("api/profile-notes")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class ProfileNotesController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Gets every profile note type.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every profile note type.</returns>
    /// <response code="200">The note types were retrieved.</response>
    [HttpGet("types")]
    [ProducesResponseType(typeof(IReadOnlyList<ProfileNoteTypeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProfileNoteTypeDto>>> GetNoteTypes(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetNoteTypesQuery(), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets the profile notes recorded against one section of a profile version.
    /// </summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="noteTypeId">The note type to filter by.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The matching notes; empty when none have been recorded.</returns>
    /// <response code="200">The notes were retrieved.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpGet("by-section")]
    [ProducesResponseType(typeof(IReadOnlyList<ProfileNoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ProfileNoteDto>>> GetNotesBySection(
        [FromQuery] Guid profileVersionId,
        [FromQuery] Guid profileSectionId,
        [FromQuery] Guid noteTypeId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetNotesBySectionQuery(profileVersionId, profileSectionId, noteTypeId),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets every profile note recorded against a profile version.
    /// </summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="noteTypeId">The note type to filter by.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The matching notes; empty when none have been recorded.</returns>
    /// <response code="200">The notes were retrieved.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpGet("by-version")]
    [ProducesResponseType(typeof(IReadOnlyList<ProfileNoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ProfileNoteDto>>> GetNotesByVersion(
        [FromQuery] Guid profileVersionId,
        [FromQuery] Guid noteTypeId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetNotesByVersionQuery(profileVersionId, noteTypeId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Inserts, updates and deletes profile notes in a single batch.
    /// </summary>
    /// <remarks>
    /// All changes are applied in a single transaction; if any fails, none are saved. Supply
    /// the <c>lastUpdated</c> row version returned with each note for every update and delete,
    /// so a concurrent edit can be detected.
    ///
    /// Sample request:
    ///
    ///     PUT /api/profile-notes
    ///     {
    ///       "profileVersionId": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
    ///       "noteTypeId": "1f2e3d4c-5b6a-7988-9a0b-1c2d3e4f5a6b",
    ///       "inserts": [
    ///         { "id": "3f4a5b6c-7d8e-9f0a-1b2c-3d4e5f607182", "noteText": "New comment", "questionReferenceAdds": [] }
    ///       ],
    ///       "updates": [],
    ///       "deletes": []
    ///     }
    /// </remarks>
    /// <param name="command">The changeset to apply.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new identifiers and row versions.</returns>
    /// <response code="200">The changeset was applied.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="409">Another user has saved one of these notes since it was read.</response>
    [HttpPut]
    [ProducesResponseType(typeof(ProfileNoteChangesetResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProfileNoteChangesetResultDto>> UpdateNotes(
        [FromBody] UpdateNotesCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        return result.ToActionResult(this);
    }
}
