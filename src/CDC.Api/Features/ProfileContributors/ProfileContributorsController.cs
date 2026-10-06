using CDC.Api.Application.Extensions;
using CDC.Api.Features.ProfileContributors.Commands;
using CDC.Api.Features.ProfileContributors.Dtos;
using CDC.Api.Features.ProfileContributors.Queries;
using CDC.Common.Contracts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.ProfileContributors;

/// <summary>
/// Read access to a profile's contributors. Replaces the legacy <c>MaintainContributors.aspx</c>
/// grid's data source.
/// </summary>
/// <param name="mediator">Dispatches queries to their handlers.</param>
[ApiController]
[Route("api/profiles")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class ProfileContributorsController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Gets one page of a profile's contributors.
    /// </summary>
    /// <param name="profileId">The profile to read.</param>
    /// <param name="pageNumber">The 1-based page to return.</param>
    /// <param name="pageSize">The number of items per page, or 0 for every contributor on a single page.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The requested page of contributors.</returns>
    /// <response code="200">The contributors were retrieved (an empty page when the profile has none).</response>
    [HttpGet("{profileId:guid}/contributors")]
    [ProducesResponseType(typeof(PagedResult<ContributorDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ContributorDto>>> GetProfileContributors(
        Guid profileId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetProfileContributorsQuery(profileId, pageNumber, pageSize), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets every role a contributor can hold on a profile, for the Role dropdown.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every contributor role.</returns>
    /// <response code="200">The roles were retrieved.</response>
    [HttpGet("/api/profile-user-roles")]
    [ProducesResponseType(typeof(IReadOnlyList<ProfileUserRoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProfileUserRoleDto>>> GetProfileUserRoles(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfileUserRolesQuery(), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets one contributor's full editable detail, for the "Edit profile contributor" panel.
    /// </summary>
    /// <param name="profileId">The profile the contributor belongs to.</param>
    /// <param name="contributorId">The contributor (user) to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The contributor's editable detail.</returns>
    /// <response code="200">The contributor was retrieved.</response>
    /// <response code="404">No such contributor exists on this profile.</response>
    [HttpGet("{profileId:guid}/contributors/{contributorId:guid}")]
    [ProducesResponseType(typeof(ContributorEditDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContributorEditDto>> GetContributorForEdit(
        Guid profileId,
        Guid contributorId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetContributorForEditQuery(profileId, contributorId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Changes a contributor's role, (for non-SSO users) full name and organisation, and the set
    /// of profile sections they may edit.
    /// </summary>
    /// <param name="profileId">The profile the contributor belongs to.</param>
    /// <param name="contributorId">The contributor (user) being updated.</param>
    /// <param name="request">The change to apply.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The contributor was updated.</response>
    /// <response code="400">The request failed a business validation rule.</response>
    /// <response code="404">No such contributor exists on this profile.</response>
    /// <response code="409">The contributor has been edited by another user since it was read.</response>
    [HttpPut("{profileId:guid}/contributors/{contributorId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> UpdateContributor(
        Guid profileId,
        Guid contributorId,
        [FromBody] UpdateContributorRequest request,
        CancellationToken cancellationToken)
    {
        var command = new UpdateContributorCommand(
            profileId,
            contributorId,
            request.RoleId,
            request.FullName,
            request.Organisation,
            request.SectionPermissionIds,
            request.LastUpdated);

        var result = await mediator.Send(command, cancellationToken);

        return result.ToNoContentActionResult(this);
    }

    /// <summary>
    /// Verifies a username for the "Add profile contributor" lookup step.
    /// </summary>
    /// <param name="userName">The username to verify.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The verification outcome.</returns>
    /// <response code="200">The username was verified.</response>
    [HttpGet("/api/profile-contributors/verify-username")]
    [ProducesResponseType(typeof(UserVerificationResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<UserVerificationResultDto>> VerifyContributorUsername(
        [FromQuery] string userName,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new VerifyContributorUsernameQuery(userName), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Adds a new profile contributor: an existing global user not yet on this profile, or a
    /// brand-new username not yet known to the system.
    /// </summary>
    /// <param name="profileId">The profile to add the contributor to.</param>
    /// <param name="request">The contributor to add.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The contributor was added.</response>
    /// <response code="400">The request failed a business validation rule, or the username is already in use.</response>
    /// <response code="409">The global user has been edited by another user since it was read.</response>
    [HttpPost("{profileId:guid}/contributors")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> AddContributor(
        Guid profileId,
        [FromBody] AddContributorRequest request,
        CancellationToken cancellationToken)
    {
        var command = new AddContributorCommand(
            profileId,
            request.ContributorId,
            request.UserName,
            request.IsSsoUser,
            request.RoleId,
            request.FullName,
            request.Organisation,
            request.SectionPermissionIds);

        var result = await mediator.Send(command, cancellationToken);

        return result.ToNoContentActionResult(this);
    }

    /// <summary>
    /// Removes a contributor from a profile.
    /// </summary>
    /// <param name="profileId">The profile to remove the contributor from.</param>
    /// <param name="contributorId">The contributor (user) being removed.</param>
    /// <param name="lastUpdated">The row version last read for this contributor.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The contributor was removed.</response>
    /// <response code="409">The contributor has been edited by another user since it was read.</response>
    [HttpDelete("{profileId:guid}/contributors/{contributorId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DeleteContributor(
        Guid profileId,
        Guid contributorId,
        [FromQuery] byte[] lastUpdated,
        CancellationToken cancellationToken)
    {
        var command = new DeleteContributorCommand(profileId, contributorId, lastUpdated);

        var result = await mediator.Send(command, cancellationToken);

        return result.ToNoContentActionResult(this);
    }
}

/// <summary>Request body for <see cref="ProfileContributorsController.AddContributor"/>.</summary>
public sealed record AddContributorRequest
{
    /// <summary>Gets the user id: the looked-up existing global user's id, or a freshly generated
    /// id for a brand-new username.</summary>
    public Guid ContributorId { get; init; }

    /// <summary>Gets the contributor's username. Only used when <see cref="ContributorId"/> does
    /// not yet exist as a global user.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the looked-up global user is an SSO user. Always
    /// <see langword="false"/> for a brand-new username.</summary>
    public bool IsSsoUser { get; init; }

    /// <summary>Gets the contributor's new role.</summary>
    public Guid RoleId { get; init; }

    /// <summary>Gets the user's full name. Ignored for an SSO user.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Gets the user's organisation. Ignored for an SSO user.</summary>
    public string Organisation { get; init; } = string.Empty;

    /// <summary>Gets the profile sections the contributor may edit.</summary>
    public IReadOnlyList<Guid> SectionPermissionIds { get; init; } = [];
}

/// <summary>Request body for <see cref="ProfileContributorsController.UpdateContributor"/>.</summary>
public sealed record UpdateContributorRequest
{
    /// <summary>Gets the contributor's new role.</summary>
    public Guid RoleId { get; init; }

    /// <summary>Gets the user's full name. Ignored for an SSO user.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Gets the user's organisation. Ignored for an SSO user.</summary>
    public string Organisation { get; init; } = string.Empty;

    /// <summary>Gets the profile sections the contributor may edit.</summary>
    public IReadOnlyList<Guid> SectionPermissionIds { get; init; } = [];

    /// <summary>Gets the row version last read for this contributor.</summary>
    public byte[] LastUpdated { get; init; } = [];
}
