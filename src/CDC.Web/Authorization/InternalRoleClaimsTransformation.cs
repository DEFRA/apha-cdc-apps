using System.Security.Claims;
using CDC.Web.Authorization.Constants;
using CDC.Web.Features.Account;
using Microsoft.AspNetCore.Authentication;

namespace CDC.Web.Authorization;

/// <summary>
/// Maps the boolean permission claims <see cref="Features.Account.InternalUserResolver"/> adds for
/// Entra ID-authenticated internal users (<see cref="ExternalUserClaimTypes.IsProfileEditor"/>,
/// <see cref="ExternalUserClaimTypes.IsPolicyProfileUser"/>) onto standard <see cref="ClaimTypes.Role"/>
/// claims, so <c>User.IsInRole(...)</c> (every policy in <c>AuthorizationPolicyRegistration</c>, via
/// <c>AllowedRolesAuthorizationHandler</c>, and the header "Menu" navigation filter) works the same
/// way it already does for CIDM-authenticated external users, whose role claims are mapped directly
/// in <c>CidmOpenIdConnectEvents.TokenValidated</c>. This is the claims transformation the doc comment
/// on <see cref="AuthorizationRoles"/> describes as replacing the old placeholder
/// <see cref="Services.CurrentUserService"/>.
/// </summary>
public sealed class InternalRoleClaimsTransformation : IClaimsTransformation
{
    /// <inheritdoc />
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is ClaimsIdentity identity && identity.IsAuthenticated)
        {
            AddRoleOnce(identity, ExternalUserClaimTypes.IsProfileEditor, AuthorizationRoles.ProfileEditor);
            AddRoleOnce(identity, ExternalUserClaimTypes.IsPolicyProfileUser, AuthorizationRoles.PolicyProfileUser);
        }

        return Task.FromResult(principal);
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
