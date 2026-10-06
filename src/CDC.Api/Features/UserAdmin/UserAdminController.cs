using CDC.Api.Application.Extensions;
using CDC.Api.Features.UserAdmin.Commands;
using CDC.Api.Features.UserAdmin.Dtos;
using CDC.Api.Features.UserAdmin.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.UserAdmin;

/// <summary>
/// User administration. Replaces the legacy <c>IUserMaintenanceService</c> WCF operations
/// behind the Maintain global users and Maintain external users screens.
/// </summary>
/// <param name="mediator">Dispatches queries and commands to their handlers.</param>
[ApiController]
[Route("api/user-admin")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class UserAdminController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Gets every internal (global) user account.
    /// </summary>
    /// <remarks>
    /// Mirrors the legacy <c>spgaGlobalUser</c> list behind Maintain global users: accounts
    /// holding the profile editor or policy profile user role, ordered by full name.
    /// </remarks>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The global users.</returns>
    /// <response code="200">The global users were retrieved.</response>
    [HttpGet("global-users")]
    [ProducesResponseType(typeof(IReadOnlyList<MaintainedUserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MaintainedUserDto>>> GetGlobalUsers(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetGlobalUsersQuery(), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets every external (single sign-on) user account.
    /// </summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The external users.</returns>
    /// <response code="200">The external users were retrieved.</response>
    [HttpGet("external-users")]
    [ProducesResponseType(typeof(IReadOnlyList<MaintainedUserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MaintainedUserDto>>> GetExternalUsers(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetExternalUsersQuery(), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets one user account.
    /// </summary>
    /// <remarks>
    /// Keep the returned <c>lastUpdated</c> row version and send it back when changing the
    /// review email subscription, so a concurrent edit can be detected.
    /// </remarks>
    /// <param name="userId">The user to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The user account.</returns>
    /// <response code="200">The user was retrieved.</response>
    /// <response code="404">No user exists with the supplied identifier.</response>
    [HttpGet("users/{userId:guid}")]
    [ProducesResponseType(typeof(MaintainedUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MaintainedUserDto>> GetUser(Guid userId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetMaintainedUserQuery(userId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Subscribes a user to, or unsubscribes them from, review notification emails.
    /// </summary>
    /// <remarks>
    /// Applies to both global and external users. Every other attribute of the account is left
    /// unchanged. Supply the <c>lastUpdated</c> row version returned by
    /// <c>GET /api/user-admin/users/{userId}</c>; if another administrator has saved in the
    /// meantime the request is rejected with 409 and nothing is written.
    ///
    /// Sample request:
    ///
    ///     PUT /api/user-admin/users/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f/review-email-subscription
    ///     {
    ///       "userId": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
    ///       "subscribedToReviewEmails": true,
    ///       "lastUpdated": "AAAAAAAAB9E="
    ///     }
    /// </remarks>
    /// <param name="userId">The user whose subscription is changing.</param>
    /// <param name="command">The subscription state to apply. The route's user id always wins.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The stored subscription state and the user's new row version.</returns>
    /// <response code="200">The subscription was stored.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="404">No user exists with the supplied identifier.</response>
    /// <response code="409">Another administrator has saved this user since it was read.</response>
    [HttpPut("users/{userId:guid}/review-email-subscription")]
    [ProducesResponseType(typeof(UpdateReviewEmailSubscriptionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UpdateReviewEmailSubscriptionResultDto>> UpdateReviewEmailSubscription(
        Guid userId,
        [FromBody] UpdateReviewEmailSubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = await mediator.Send(command with { UserId = userId }, cancellationToken);

        return result.ToActionResult(this);
    }
}
