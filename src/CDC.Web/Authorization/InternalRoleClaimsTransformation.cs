using System.Security.Claims;
using CDC.Auth.Cidm;
using CDC.Web.Authorization.Constants;
using CDC.Web.Features.Account;
using Microsoft.AspNetCore.Authentication;

namespace CDC.Web.Authorization;

/// <summary>
/// Maps the boolean permission claims <see cref="Features.Account.InternalUserResolver"/> adds for
/// Entra ID-authenticated internal users (<see cref="ExternalUserClaimTypes.IsProfileEditor"/>,
/// <see cref="ExternalUserClaimTypes.IsPolicyProfileUser"/>) onto standard <see cref="ClaimTypes.Role"/>
/// claims, so <c>User.IsInRole(...)</c> (every policy in <c>AuthorizationPolicyRegistration</c>, via
/// <c>AllowedRolesAuthorizationHandler</c>, and the header "Menu" navigation filter) can tell an internal
/// user's global role.
/// </summary>
/// <remarks>
/// An external (CIDM) user holds no global role: their permissions are defined at profile level, in the database, and CDC.Api
/// applies them. The role claims in a CIDM token describe the person's organisation, are not ours to grant, and are removed
/// here so that one can never satisfy a policy - a CIDM role that happened to be called "ProfileEditor" would otherwise make
/// an external user an editor.
/// </remarks>
public sealed class InternalRoleClaimsTransformation : IClaimsTransformation
{
    /// <inheritdoc />
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is ClaimsIdentity identity && identity.IsAuthenticated)
        {
            if (IsExternalUser(identity))
            {
                RemoveRoleClaims(identity);
            }
            else
            {
                AddRoleOnce(identity, ExternalUserClaimTypes.IsProfileEditor, AuthorizationRoles.ProfileEditor);
                AddRoleOnce(identity, ExternalUserClaimTypes.IsPolicyProfileUser, AuthorizationRoles.PolicyProfileUser);
            }
        }

        return Task.FromResult(principal);
    }

    private static bool IsExternalUser(ClaimsIdentity identity) =>
        identity.HasClaim(ExternalUserClaimTypes.AuthenticationProvider, CidmAuthenticationDefaults.AuthenticationScheme);

    private static void RemoveRoleClaims(ClaimsIdentity identity)
    {
        foreach (var roleClaim in identity.FindAll(ClaimTypes.Role).ToList())
        {
            identity.RemoveClaim(roleClaim);
        }
    }

    // Runs on every request for the lifetime of the cookie session, so it must not add a
    // duplicate role claim each time.
    private static void AddRoleOnce(ClaimsIdentity identity, string permissionClaimType, string roleName)
    {
        if (identity.HasClaim(claim => claim.Type == ClaimTypes.Role && claim.Value == roleName))
        {
            return;
        }

        if (bool.TryParse(identity.FindFirst(permissionClaimType)?.Value, out var isGranted) && isGranted)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, roleName));
        }
    }
}
