using CDC.Auth.Cidm;
using CDC.Auth.Entra;

namespace CDC.Web.Features.Account;

/// <summary>
/// The claims a resolved user's session carries, built in one place so that a session started without the identity provider
/// (LoadTesting) carries exactly what the Entra ID and CIDM sign-ins add.
/// </summary>
internal static class ResolvedUserClaims
{
    /// <summary>Builds the claims of an internal user, as added after an Entra ID sign-in.</summary>
    /// <param name="id">The user's <c>[dbo].[User].Id</c>.</param>
    /// <param name="fullName">The user's display name.</param>
    /// <param name="isProfileEditor">Whether the user can author and publish profiles.</param>
    /// <param name="isPolicyProfileUser">Whether the user is a policy profile user.</param>
    /// <returns>The claims, keyed by claim type.</returns>
    public static Dictionary<string, string> ForInternalUser(Guid id, string fullName, bool isProfileEditor, bool isPolicyProfileUser) =>
        new()
        {
            [ExternalUserClaimTypes.InternalUserId] = id.ToString(),
            [ExternalUserClaimTypes.FullName] = fullName,
            [ExternalUserClaimTypes.AuthenticationProvider] = EntraAuthenticationDefaults.AuthenticationScheme,
            // InternalRoleClaimsTransformation turns these two flags into role claims on every request.
            [ExternalUserClaimTypes.IsProfileEditor] = isProfileEditor.ToString(),
            [ExternalUserClaimTypes.IsPolicyProfileUser] = isPolicyProfileUser.ToString()
        };

    /// <summary>Builds the claims of an external user, as added after a CIDM sign-in.</summary>
    /// <param name="id">The user's <c>[dbo].[User].Id</c>.</param>
    /// <param name="fullName">The user's display name.</param>
    /// <param name="isInternalProfileEditor">Whether the user is an internal profile editor signing in through CIDM.</param>
    /// <returns>The claims, keyed by claim type.</returns>
    public static Dictionary<string, string> ForExternalUser(Guid id, string fullName, bool isInternalProfileEditor)
    {
        var claims = new Dictionary<string, string>
        {
            [ExternalUserClaimTypes.InternalUserId] = id.ToString(),
            [ExternalUserClaimTypes.FullName] = fullName,
            [ExternalUserClaimTypes.AuthenticationProvider] = CidmAuthenticationDefaults.AuthenticationScheme
        };

        if (isInternalProfileEditor)
        {
            claims[ExternalUserClaimTypes.IsInternalProfileEditor] = bool.TrueString;
        }

        return claims;
    }
}
