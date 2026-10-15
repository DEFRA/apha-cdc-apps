using CDC.Api.Application.Extensions;
using CDC.Api.Features.GlobalUsers.Commands;
using CDC.Api.Features.GlobalUsers.Dtos;
using CDC.Api.Features.GlobalUsers.Queries;
using CDC.Common.Contracts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.GlobalUsers;

/// <summary>
/// Maintains global users and their system roles. Replaces the legacy <c>MaintainGlobalUsers.aspx</c>
/// data source and save/delete handlers.
/// </summary>
/// <param name="mediator">Dispatches queries and commands to their handlers.</param>
[ApiController]
[Route("api/global-users")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class GlobalUsersController(ISender mediator) : ControllerBase
{
    /// <summary>Gets one page of global users.</summary>
    /// <param name="pageNumber">The 1-based page to return.</param>
    /// <param name="pageSize">Items per page, or 0 for every user on a single page.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The users were retrieved.</response>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<GlobalUserDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<GlobalUserDto>>> GetGlobalUsers(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await mediator.Send(new GetGlobalUsersQuery(pageNumber, pageSize), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Looks a username up for the "Add global user" step.</summary>
    /// <param name="userName">The username to look up.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The username was classified.</response>
    [HttpGet("lookup")]
    [ProducesResponseType(typeof(GlobalUserLookupResult), StatusCodes.Status200OK)]
    public async Task<ActionResult<GlobalUserLookupResult>> Lookup([FromQuery] string userName, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new LookupGlobalUserQuery(userName ?? string.Empty), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Adds a global user: an existing user given a global role, or a brand-new username.</summary>
    /// <param name="request">The user to add.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The user was saved.</response>
    /// <response code="400">The request failed a business validation rule, or the username is in use.</response>
    /// <response code="409">The user has been edited by someone else since it was read.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> AddGlobalUser([FromBody] SaveGlobalUserRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(ToCommand(request.Id, isNew: true, request), cancellationToken);

        return result.ToNoContentActionResult(this);
    }

    /// <summary>Changes a global user's role, full name and organisation.</summary>
    /// <param name="id">The user being updated.</param>
    /// <param name="request">The change to apply.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The user was saved.</response>
    /// <response code="400">The request failed a business validation rule.</response>
    /// <response code="404">No such user exists.</response>
    /// <response code="409">The user has been edited by someone else since it was read.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> UpdateGlobalUser(Guid id, [FromBody] SaveGlobalUserRequest request, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(ToCommand(id, isNew: false, request), cancellationToken);

        return result.ToNoContentActionResult(this);
    }

    /// <summary>Revokes a user's global role.</summary>
    /// <param name="id">The user whose role is revoked.</param>
    /// <param name="lastUpdated">The row version last read for this user.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The role was revoked.</response>
    /// <response code="409">The user has been edited by someone else since it was read.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult> DeleteGlobalUser(Guid id, [FromQuery] byte[] lastUpdated, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteGlobalUserCommand(id, lastUpdated), cancellationToken);

        return result.ToNoContentActionResult(this);
    }

    private static SaveGlobalUserCommand ToCommand(Guid id, bool isNew, SaveGlobalUserRequest request) => new(
        id,
        isNew,
        request.UserName,
        request.FullName,
        request.Organisation,
        request.Role,
        request.SubscribedToReviewEmails,
        request.LastUpdated);
}

/// <summary>Request body for adding or editing a global user.</summary>
public sealed record SaveGlobalUserRequest
{
    /// <summary>Gets the user's identifier. Ignored by the edit endpoint, which uses the route id.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the username. Only used when the user does not yet exist.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Gets the user's full name.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Gets the user's organisation.</summary>
    public string Organisation { get; init; } = string.Empty;

    /// <summary>Gets the system role to assign.</summary>
    public GlobalUserRole Role { get; init; }

    /// <summary>Gets a value indicating whether the user receives review notification emails.</summary>
    public bool SubscribedToReviewEmails { get; init; }

    /// <summary>Gets the row version last read for this user.</summary>
    public byte[] LastUpdated { get; init; } = [];
}
