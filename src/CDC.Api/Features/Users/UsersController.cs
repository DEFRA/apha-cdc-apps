using CDC.Api.Application.Extensions;
using CDC.Api.Features.Users.Commands;
using CDC.Api.Features.Users.Dtos;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.Users;

/// <summary>
/// Resolves the <c>[dbo].[User]</c> row for a CIDM-authenticated external user or an Entra
/// ID-authenticated internal user. Called by CDC.Web once per sign-in, from the validated
/// id_token's claims. Also finds a user by email address, read-only, for CDC.Web's LoadTesting sign-in.
/// </summary>
/// <param name="mediator">Dispatches the resolve and lookup commands.</param>
[ApiController]
[Route("api/users")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class UsersController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Resolves (or provisions) the external user matching the supplied CIDM claims.
    /// </summary>
    /// <param name="command">The CIDM claims to resolve against.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The resolved user, or a problem response when not permitted.</returns>
    [HttpPost("external/resolve")]
    [ProducesResponseType(typeof(ExternalUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ExternalUserDto>> ResolveExternalUser(
        ResolveExternalUserCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Resolves the internal user matching the supplied Entra ID claims, creating the user with no
    /// privileges when no row matches.
    /// </summary>
    /// <param name="command">The Entra ID claims to resolve against.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The resolved or newly created user.</returns>
    [HttpPost("internal/resolve")]
    [ProducesResponseType(typeof(InternalUserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<InternalUserDto>> ResolveInternalUser(
        ResolveInternalUserCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Finds the internal user holding an email address, so CDC.Web's LoadTesting can sign that user in without Entra ID. Read-only:
    /// no user is created and no identifier is recorded.
    /// </summary>
    /// <param name="command">The email address to look up.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The user, or a problem response when none, several, or an external user holds the address.</returns>
    [HttpPost("internal/lookup")]
    [ProducesResponseType(typeof(UserLookupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<ActionResult<UserLookupDto>> LookUpInternalUser(
        LookUpUserByEmailCommand command,
        CancellationToken cancellationToken) =>
        LookUpAsync(command with { Kind = UserLookupKind.Internal }, cancellationToken);

    /// <summary>
    /// Finds the external user holding an email address, so CDC.Web's LoadTesting can sign that user in without CIDM. Read-only:
    /// no user is linked or created and no identifier is recorded.
    /// </summary>
    /// <param name="command">The email address to look up.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The user, or a problem response when none, several, or a user who may not sign in externally holds the address.</returns>
    [HttpPost("external/lookup")]
    [ProducesResponseType(typeof(UserLookupDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<ActionResult<UserLookupDto>> LookUpExternalUser(
        LookUpUserByEmailCommand command,
        CancellationToken cancellationToken) =>
        LookUpAsync(command with { Kind = UserLookupKind.External }, cancellationToken);

    private async Task<ActionResult<UserLookupDto>> LookUpAsync(LookUpUserByEmailCommand command, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        return result.ToActionResult(this);
    }
}
