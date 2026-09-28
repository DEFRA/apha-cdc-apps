using CDC.Api.Application.Extensions;
using CDC.Api.Features.ProfileManagement.Commands;
using CDC.Api.Features.ProfileManagement.Dtos;
using CDC.Api.Features.ProfileManagement.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.ProfileManagement;

/// <summary>
/// Profile and profile version lifecycle management. Replaces the legacy
/// <c>IProfileManagementService</c> WCF endpoint.
/// </summary>
/// <param name="mediator">Dispatches queries and commands to their handlers.</param>
[ApiController]
[Route("api/profiles")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class ProfileManagementController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Gets a profile's attributes, current version pointers and affected species.
    /// </summary>
    /// <param name="profileId">The profile to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The profile's attributes.</returns>
    /// <response code="200">The profile was retrieved.</response>
    /// <response code="404">No profile exists with the supplied identifier.</response>
    [HttpGet("{profileId:guid}/attributes")]
    [ProducesResponseType(typeof(ProfileAttributesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfileAttributesDto>> GetProfileAttributes(
        Guid profileId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfileAttributesQuery(profileId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets default values for a new profile or "what-if" scenario, sourced from the profile
    /// version being cloned.
    /// </summary>
    /// <param name="cloneProfileVersionId">The profile version to read defaults from.</param>
    /// <param name="isWhatIfScenario">Whether the new profile will be a "what-if" scenario.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The default values.</returns>
    /// <response code="200">The defaults were retrieved.</response>
    /// <response code="400">No source profile version was supplied.</response>
    /// <response code="404">No profile version exists with the supplied identifier.</response>
    [HttpGet("defaults")]
    [ProducesResponseType(typeof(NewProfileDefaultsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<NewProfileDefaultsDto>> GetNewProfileDefaults(
        [FromQuery] Guid cloneProfileVersionId,
        [FromQuery] bool isWhatIfScenario,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetNewProfileDefaultsQuery(cloneProfileVersionId, isWhatIfScenario),
            cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets the name and active state of one species.
    /// </summary>
    /// <remarks>
    /// Mirrors the legacy <c>GetAffectedSpecies</c> operation, which looks a species up
    /// directly by identifier rather than by profile.
    /// </remarks>
    /// <param name="speciesId">The species to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The species' name and active state.</returns>
    /// <response code="200">The species was retrieved.</response>
    /// <response code="404">No species exists with the supplied identifier.</response>
    [HttpGet("species/{speciesId:guid}")]
    [ProducesResponseType(typeof(AffectedSpeciesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AffectedSpeciesDto>> GetAffectedSpecies(Guid speciesId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAffectedSpeciesQuery(speciesId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets every profile status a profile can be set to.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every profile status.</returns>
    /// <response code="200">The profile status types were retrieved.</response>
    [HttpGet("status-types")]
    [ProducesResponseType(typeof(IReadOnlyList<ProfileStatusTypeDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProfileStatusTypeDto>>> GetProfileStatusTypes(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfileStatusTypesQuery(), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Creates a new profile, its initial draft version, and any affected species.
    /// </summary>
    /// <param name="command">The profile to create.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new profile's identifier and row version.</returns>
    /// <response code="201">The profile was created.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CreateProfileResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateProfileResultDto>> CreateProfile(
        [FromBody] CreateProfileCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        return result.Status == Domain.Common.ResultStatus.Success
            ? CreatedAtAction(nameof(GetProfileAttributes), new { profileId = result.Value.NewProfileId }, result.Value)
            : result.ToActionResult(this);
    }

    /// <summary>
    /// Updates an existing profile's attributes and affected species.
    /// </summary>
    /// <param name="profileId">The profile to update.</param>
    /// <param name="command">The changes to apply.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>No content.</returns>
    /// <response code="204">The profile was updated.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="409">Another user has saved this profile since it was read.</response>
    [HttpPut("{profileId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateProfileAttributes(
        Guid profileId,
        [FromBody] UpdateProfileAttributesCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command with { Id = profileId }, cancellationToken);

        return result.Status == Domain.Common.ResultStatus.Success
            ? NoContent()
            : result.ToActionResult(this).Result!;
    }

    /// <summary>
    /// Creates a new version of a profile.
    /// </summary>
    /// <remarks>
    /// The source version must be the latest version of its profile. A published version
    /// cannot be based on an already-published version, a draft cannot be made public, and the
    /// source version must have at least one active profiled species. Publishing recalculates
    /// prioritisation scores.
    /// </remarks>
    /// <param name="command">The profile version to base the new version on.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new profile version's identifier.</returns>
    /// <response code="201">The profile version was created.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="409">The source version is not eligible for a new version.</response>
    [HttpPost("versions")]
    [ProducesResponseType(typeof(NewProfileVersionResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<NewProfileVersionResultDto>> CreateNewProfileVersion(
        [FromBody] CreateNewProfileVersionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        return result.Status == Domain.Common.ResultStatus.Success
            ? CreatedAtAction(
                nameof(GetProfileAttributes),
                new { profileId = result.Value.NewProfileVersionId },
                result.Value)
            : result.ToActionResult(this);
    }

    /// <summary>
    /// Deletes a profile version and its affected species.
    /// </summary>
    /// <param name="profileVersionId">The profile version to delete.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Which version is now latest.</returns>
    /// <response code="200">The profile version was deleted.</response>
    /// <response code="404">No profile version exists with the supplied identifier.</response>
    [HttpDelete("versions/{profileVersionId:guid}")]
    [ProducesResponseType(typeof(DeleteProfileVersionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DeleteProfileVersionResultDto>> DeleteProfileVersion(
        Guid profileVersionId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteProfileVersionCommand(profileVersionId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Toggles a profile version's public visibility flag.
    /// </summary>
    /// <param name="profileVersionId">The profile version to toggle.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>No content.</returns>
    /// <response code="204">The public access flag was toggled.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpPut("versions/{profileVersionId:guid}/public-access")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetProfileVersionPublicAccess(Guid profileVersionId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SetProfileVersionPublicAccessCommand(profileVersionId), cancellationToken);

        return result.ToNoContentActionResult(this);
    }

    /// <summary>
    /// Updates a profile's status.
    /// </summary>
    /// <param name="profileId">The profile to update.</param>
    /// <param name="command">The status to set.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>No content.</returns>
    /// <response code="204">The status was updated.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="404">No profile status exists with the supplied identifier.</response>
    [HttpPut("{profileId:guid}/status")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateProfileStatus(
        Guid profileId,
        [FromBody] UpdateProfileStatusCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command with { ProfileId = profileId }, cancellationToken);

        return result.ToNoContentActionResult(this);
    }
}
