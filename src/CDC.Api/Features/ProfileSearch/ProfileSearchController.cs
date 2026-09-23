using CDC.Api.Application.Extensions;
using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileSearch.Dtos;
using CDC.Api.Features.ProfileSearch.Interfaces;
using CDC.Api.Features.ProfileSearch.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.ProfileSearch;

/// <summary>
/// Retrieves profile search results and profile version details for the Surveillance Profiles service.
/// </summary>
/// <param name="mediator">Dispatches profile search queries.</param>
/// <param name="profileSearchService">Profile search data service.</param>
[ApiController]
[Route("api/profile-search")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class ProfileSearchController(ISender mediator, IProfileSearchService profileSearchService) : ControllerBase
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

    /// <summary>
    /// Searches for profiles with optional filtering and text search.
    /// </summary>
    /// <param name="searchText">Optional text to search in profile titles.</param>
    /// <param name="displayPublished">Include published versions (default: true).</param>
    /// <param name="displayDraft">Include draft versions (default: false).</param>
    /// <param name="displayScenarios">Include scenario versions (default: false).</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>List of profiles matching the search criteria.</returns>
    [HttpGet("search")]
    [ProducesResponseType(typeof(IReadOnlyList<ProfileSearchResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProfileSearchResultDto>>> SearchProfiles(
        [FromQuery] string? searchText = null,
        [FromQuery] bool displayPublished = true,
        [FromQuery] bool displayDraft = false,
        [FromQuery] bool displayScenarios = false,
        CancellationToken cancellationToken = default)
    {
        var results = await profileSearchService.GetProfileSearchResultsAsync(
            searchText,
            displayPublished,
            displayDraft,
            displayScenarios,
            cancellationToken);

        return Ok(results);
    }

    /// <summary>
    /// Gets profiles starting with a specific letter.
    /// </summary>
    /// <param name="letter">The letter to filter by (or "All" for all profiles).</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Profiles starting with the specified letter.</returns>
    [HttpGet("search/by-letter/{letter}")]
    [ProducesResponseType(typeof(IReadOnlyList<ProfileSearchResultDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProfileSearchResultDto>>> SearchProfilesByLetter(
        string letter,
        CancellationToken cancellationToken = default)
    {
        var results = await profileSearchService.GetProfilesByLetterAsync(letter, cancellationToken);

        return Ok(results);
    }
}
