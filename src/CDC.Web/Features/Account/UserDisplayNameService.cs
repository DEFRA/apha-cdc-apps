using System.Security.Claims;
using CDC.Auth.Cidm;

namespace CDC.Web.Features.Account;

/// <summary>
/// Default <see cref="IUserDisplayNameService"/>. Detects which identity provider authenticated
/// the current user via <see cref="ExternalUserClaimTypes.AuthenticationProvider"/> (added by
/// <see cref="InternalUserResolver"/>/<see cref="ExternalUserResolver"/> at sign-in) and delegates
/// to the matching provider-specific lookup, so Razor views never branch on provider themselves.
/// </summary>
public sealed class UserDisplayNameService : IUserDisplayNameService
{
    /// <summary>Shown when no usable claim is found, or the user is not authenticated.</summary>
    public const string FallbackDisplayName = "Signed in user";

    /// <inheritdoc />
    public string GetDisplayName(ClaimsPrincipal? user) // NOSONAR - must be an instance method to implement IUserDisplayNameService for DI
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return FallbackDisplayName;
        }

        var isExternalUser = user.HasClaim(ExternalUserClaimTypes.AuthenticationProvider, CidmAuthenticationDefaults.AuthenticationScheme);

        return isExternalUser ? GetExternalDisplayName(user) : GetInternalDisplayName(user);
    }

    // Entra ID (internal users): claim priority per spec - Name, DisplayName, Given Name + Surname,
    // Preferred Username, Email - falling back to the universal placeholder if none are usable.
    private static string GetInternalDisplayName(ClaimsPrincipal user) =>
        FirstNonEmpty(
            () => user.FindFirst(ClaimTypes.Name)?.Value ?? user.FindFirst("name")?.Value,
            () => user.FindFirst("displayname")?.Value,
            () => JoinNonEmpty(" ", GivenName(user), Surname(user)),
            () => user.FindFirst("preferred_username")?.Value,
            () => user.FindFirst(ClaimTypes.Email)?.Value ?? user.FindFirst("email")?.Value)
        ?? FallbackDisplayName;

    private static string? GivenName(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.GivenName)?.Value ?? user.FindFirst("given_name")?.Value;

    private static string? Surname(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.Surname)?.Value ?? user.FindFirst("family_name")?.Value;

    // EXTENSION POINT: CIDM / GOV.UK One Login claim-based display name retrieval is not yet
    // implemented. Today this reads only the FullName claim ExternalUserResolver already populates
    // from CDC.Api's resolved [dbo].[User] row. When direct GOV.UK One Login profile claims need to
    // be read, add them as further FirstNonEmpty candidates here only - GetInternalDisplayName and
    // GetDisplayName's provider detection do not need to change.
    private static string GetExternalDisplayName(ClaimsPrincipal user) =>
        FirstNonEmpty(() => user.FindFirst(ExternalUserClaimTypes.FullName)?.Value)
        ?? FallbackDisplayName;

    private static string? FirstNonEmpty(params Func<string?>[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var value = candidate();
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static string? JoinNonEmpty(string separator, params string?[] parts)
    {
        var nonEmpty = parts.Where(part => !string.IsNullOrWhiteSpace(part)).ToArray();
        return nonEmpty.Length == 0 ? null : string.Join(separator, nonEmpty);
    }
}
