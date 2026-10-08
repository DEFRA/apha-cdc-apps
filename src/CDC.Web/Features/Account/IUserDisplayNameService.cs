using System.Security.Claims;

namespace CDC.Web.Features.Account;

/// <summary>
/// Resolves the signed-in user's display name for the shared page header, independent of which
/// identity provider authenticated them. See <see cref="UserDisplayNameService"/>.
/// </summary>
public interface IUserDisplayNameService
{
    /// <summary>Gets the display name to show for the given principal, or a safe fallback.</summary>
    /// <param name="user">The current request's principal, or <see langword="null"/>.</param>
    /// <returns>A non-empty display name, never <see langword="null"/>.</returns>
    string GetDisplayName(ClaimsPrincipal? user);
}
