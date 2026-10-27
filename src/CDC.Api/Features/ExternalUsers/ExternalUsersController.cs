using System.Text.Json.Serialization;
using CDC.Api.Application;
using CDC.Api.Application.Extensions;
using CDC.Api.Features.ExternalUsers.Commands;
using CDC.Api.Features.ExternalUsers.Dtos;
using CDC.Api.Features.ExternalUsers.Queries;
using CDC.Common.Contracts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.ExternalUsers;

/// <summary>
/// Maintains external users' review email subscriptions. Replaces the legacy <c>MaintainExternalUsers.aspx</c>
/// subscription checkbox.
/// </summary>
/// <param name="mediator">Dispatches queries and commands to their handlers.</param>
[ApiController]
[Route("api/external-users")]
[RequireGlobalRole(GlobalRole.ProfileEditor)]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class ExternalUsersController(ISender mediator) : ControllerBase
{
    /// <summary>Gets one page of external users.</summary>
    /// <param name="pageNumber">The 1-based page to return.</param>
    /// <param name="pageSize">Items per page, or 0 for every user on a single page.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The users were retrieved.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ExternalUserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ExternalUserDto>>> GetExternalUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetExternalUsersQuery(pageNumber, pageSize), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Subscribes or unsubscribes an external user from review notification emails.</summary>
    /// <param name="id">The user being changed.</param>
    /// <param name="request">The subscription to apply.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The subscription was saved.</response>
    /// <response code="404">No such user exists.</response>
    [HttpPut("{id:guid}/review-email-subscription")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SetReviewEmailSubscription(
        Guid id, [FromBody] SetReviewEmailSubscriptionRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SetExternalUserSubscriptionCommand(id, request.SubscribedToReviewEmails), cancellationToken);

        return result.ToNoContentActionResult(this);
    }

    /// <summary>Gets the profiles an external user can access, and the profiles still available to grant.</summary>
    /// <param name="id">The external user.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The profile access was retrieved.</response>
    /// <response code="404">No such external user exists.</response>
    [HttpGet("{id:guid}/profile-access")]
    [ProducesResponseType(typeof(ExternalUserProfileAccessContract), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ExternalUserProfileAccessContract>> GetProfileAccess(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetExternalUserProfileAccessQuery(id), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Grants an external user a role and section permissions on a profile, or changes an existing grant.</summary>
    /// <param name="id">The external user.</param>
    /// <param name="profileId">The profile being granted.</param>
    /// <param name="request">The role and sections to grant.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The access was saved.</response>
    /// <response code="400">The role or sections are invalid.</response>
    /// <response code="404">No such external user exists.</response>
    /// <response code="409">The access was changed by another user.</response>
    [HttpPut("{id:guid}/profile-access/{profileId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> SaveProfileAccess(
        Guid id, Guid profileId, [FromBody] SaveExternalUserProfileAccessContract request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new SaveExternalUserProfileAccessCommand(id, profileId, request.RoleId, request.SectionPermissionIds), cancellationToken);

        return result.ToNoContentActionResult(this);
    }

    /// <summary>Removes an external user's access to a profile.</summary>
    /// <param name="id">The external user.</param>
    /// <param name="profileId">The profile to remove access to.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The access was removed.</response>
    /// <response code="404">The user or the access does not exist.</response>
    /// <response code="409">The access was changed by another user.</response>
    [HttpDelete("{id:guid}/profile-access/{profileId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DeleteProfileAccess(Guid id, Guid profileId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteExternalUserProfileAccessCommand(id, profileId), cancellationToken);

        return result.ToNoContentActionResult(this);
    }
}

/// <summary>Request body for changing an external user's review email subscription.</summary>
public sealed record SetReviewEmailSubscriptionRequest
{
    /// <summary>Gets a value indicating whether the user receives review notification emails.</summary>
    [JsonRequired]
    public bool SubscribedToReviewEmails { get; init; }
}
