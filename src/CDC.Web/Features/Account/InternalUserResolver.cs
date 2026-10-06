using System.Security.Claims;
using CDC.Auth.Entra;
using CDC.Auth.Entra.Claims;
using CDC.Auth.Entra.Events;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Features.Account;

/// <summary>
/// Resolves an Entra ID-authenticated internal user against CDC.Api's <c>[dbo].[User]</c> row,
/// once per sign-in (called from <c>EntraOpenIdConnectEvents.TokenValidated</c>, before the local
/// cookie is written). Adds <see cref="ExternalUserClaimTypes.InternalUserId"/> and
/// <see cref="ExternalUserClaimTypes.FullName"/> so the rest of the app - including the page
/// header - never needs to call CDC.Api again to know who the user is.
/// </summary>
/// <param name="apiClient">Calls CDC.Api's internal-user resolve endpoint.</param>
public sealed class InternalUserResolver(IApiClient apiClient) : IEntraInternalUserResolver
{
    /// <inheritdoc />
    public async Task<EntraInternalUserResolution> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // 'oid' (directory object id) is the Microsoft-recommended stable per-user identifier -
        // unlike 'sub', which is pairwise/per-application by default on the v2.0 endpoint. Default
        // inbound JWT claim mapping sometimes remaps it to the long schema URI, checked as a
        // fallback; raw "oid" is checked first in case mapping is ever turned off.
        var entraSsoIdClaim = principal.FindFirst(EntraClaimTypes.ObjectId)?.Value
            ?? principal.FindFirst(EntraClaimTypes.ObjectIdLongClaimUri)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(entraSsoIdClaim, out var entraSsoId))
        {
            return EntraInternalUserResolution.Deny("/Account/NotPermitted");
        }

        // Requires the Entra ID app registration to configure onprem_samaccountname and
        // onprem_domainname as optional ID token claims (Azure Portal: App registration -> Token
        // configuration -> Add optional claim -> ID token) - they are not present by default.
        var samAccountName = principal.FindFirst(EntraClaimTypes.OnPremisesSamAccountName)?.Value ?? string.Empty;

        // AD Connect can sync this as either NetBIOS ("DEFRA") or FQDN ("DT2.local") depending on
        // the tenant's sync configuration - [dbo].[User].UserName was always populated with the
        // short NetBIOS-style prefix (from the legacy Windows-auth days), so truncate at the first
        // '.' to match regardless of which form this tenant actually sends.
        var rawDomainName = principal.FindFirst(EntraClaimTypes.OnPremisesDomainName)?.Value ?? string.Empty;
        var domainName = rawDomainName.Split('.')[0];
        var userName = !string.IsNullOrEmpty(domainName) && !string.IsNullOrEmpty(samAccountName)
            ? $"{domainName}\\{samAccountName}"
            : samAccountName;

        if (string.IsNullOrEmpty(userName))
        {
            return EntraInternalUserResolution.Deny("/Account/NotPermitted");
        }

        // Standard OIDC 'name' claim (granted by the 'profile' scope) - used only as the FullName
        // for a user with no matching [dbo].[User] row (legacy's "limited access" internal user);
        // ignored once a row is matched, since FullName then comes from that row instead.
        var fullName = principal.FindFirst(ClaimTypes.Name)?.Value ?? principal.FindFirst("name")?.Value ?? string.Empty;

        var request = new ResolveInternalUserRequestDto
        {
            SsoUserIdInt = entraSsoId,
            UserName = userName,
            FullName = fullName
        };

        var result = await apiClient.ResolveInternalUserAsync(request, cancellationToken);

        return result switch
        {
            { Outcome: ResolveInternalUserOutcome.Success, User: not null } => EntraInternalUserResolution.Allow(
                new Dictionary<string, string>
                {
                    [ExternalUserClaimTypes.InternalUserId] = result.User.Id.ToString(),
                    [ExternalUserClaimTypes.FullName] = result.User.FullName,
                    [ExternalUserClaimTypes.AuthenticationProvider] = EntraAuthenticationDefaults.AuthenticationScheme,
                    [ExternalUserClaimTypes.IsProfileEditor] = result.User.IsProfileEditor.ToString(),
                    [ExternalUserClaimTypes.IsPolicyProfileUser] = result.User.IsPolicyProfileUser.ToString()
                }),
            { Outcome: ResolveInternalUserOutcome.NotPermitted } => EntraInternalUserResolution.Deny("/Account/NotPermitted"),
            _ => EntraInternalUserResolution.Deny("/Landing/Error")
        };
    }
}
