using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.Users.Commands;

namespace CDC.Api.Features.Users.Interfaces;

/// <summary>
/// Resolves the <c>[dbo].[User]</c> row for an authenticated CIDM (external) or Entra ID
/// (internal) user.
/// </summary>
public interface IUserService
{
    /// <summary>
    /// Finds the user by SsoUserIdExt first. If no row has that SsoUserIdExt yet - true for every
    /// user's first CIDM sign-in - falls back to matching by email. A row found by email is only
    /// linked to this SsoUserIdExt if it is already an external user (has a legacy SsoUserId);
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

    /// <summary>
    /// Finds the user by SsoUserIdInt first. If no row has that SsoUserIdInt yet - true for every
    /// user's first Entra ID sign-in - falls back to matching by user name, backfilling SsoUserIdInt
    /// on that row so every subsequent sign-in matches directly. If no row matches either value, a
    /// new internal user is created with no privileges (<c>IsProfileEditor</c> and
    /// <c>IsPolicyProfileUser</c> both <see langword="false"/>) until a role is granted.
    /// </summary>
    /// <param name="command">The Entra ID claims to resolve against.</param>
    /// <param name="cancellationToken">Cancels the database calls.</param>
    /// <returns>The matched or newly created user.</returns>
    Task<Result<InternalUser>> ResolveInternalUserAsync(ResolveInternalUserCommand command, CancellationToken cancellationToken);
}
