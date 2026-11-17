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

    /// <summary>
    /// Finds the one user holding an email address and checks that the user could sign in as the kind of user asked for, so
    /// CDC.Web's LoadTesting can sign that user in without the identity provider. Read-only: nothing is linked, backfilled or created.
    /// </summary>
    /// <param name="command">The email address and the kind of sign-in wanted.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>
    /// The user on success; <see cref="ResultStatus.NotFound"/> when no user holds the address; <see cref="ResultStatus.Conflict"/>
    /// when several do; <see cref="ResultStatus.Forbidden"/> when the user could not sign in as that kind of user.
    /// </returns>
    Task<Result<ExternalUser>> LookUpByEmailAsync(LookUpUserByEmailCommand command, CancellationToken cancellationToken);
}
