using CDC.Api.Application.Extensions;
using CDC.Api.Features.Users.Commands;
using CDC.Api.Features.Users.Dtos;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.Users;

/// <summary>
/// Resolves the <c>[dbo].[User]</c> row for a CIDM-authenticated external user. Called by
/// CDC.Web once per sign-in, from the validated id_token's claims.
/// </summary>
/// <param name="mediator">Dispatches the resolve command.</param>
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
}
