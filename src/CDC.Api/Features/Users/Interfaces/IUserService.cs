using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.Users.Commands;

namespace CDC.Api.Features.Users.Interfaces;

/// <summary>
/// Resolves the <c>[dbo].[User]</c> row for an authenticated CIDM (external) user. Internal
/// users are resolved through a different mechanism and are not handled here.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Finds the user by CidmSsoId first. If no row has that CidmSsoId yet - true for every
    /// user's first CIDM sign-in - falls back to matching by email. A row found by email is only
    /// linked to this CidmSsoId if it is already an external user (has a legacy SsoUserId);
    /// otherwise the sign-in is denied. If no row matches either value, a new external user is
    /// provisioned.
    /// </summary>
    /// <param name="command">The CIDM claims to resolve against.</param>
    /// <param name="cancellationToken">Cancels the database calls.</param>
    /// <returns>
    /// The resolved user on success; <see cref="ResultStatus.Forbidden"/> when the email belongs
    /// to an existing non-external user.
    /// </returns>
    Task<Result<ExternalUser>> ResolveExternalUserAsync(ResolveExternalUserCommand command, CancellationToken cancellationToken);
}
