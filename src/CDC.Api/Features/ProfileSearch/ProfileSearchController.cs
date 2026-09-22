using CDC.Api.Application.Extensions;
using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileSearch.Dtos;
using CDC.Api.Features.ProfileSearch.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.ProfileSearch;

/// <summary>
/// Retrieves profile search results and profile version details for the Surveillance Profiles service.
/// </summary>
/// <param name="mediator">Dispatches profile search queries.</param>
[ApiController]
[Route("api/profile-search")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class ProfileSearchController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Gets every profile summary.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A list of profile summaries.</returns>
    [HttpGet("profiles")]
    [ProducesResponseType(typeof(IReadOnlyList<ProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProfileDto>>> GetAllProfiles(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAllProfilesQuery(), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets a single profile version by identifier.
    /// </summary>
    /// <param name="profileVersionId">Profile version identifier.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The profile version if it exists.</returns>
    [HttpGet("versions/{profileVersionId:guid}")]
    [ProducesResponseType(typeof(ProfileVersionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfileVersionDto>> GetProfileVersion(
        Guid profileVersionId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfileVersionQuery(profileVersionId), cancellationToken);

        return result.ToActionResult(this);
    }
}
