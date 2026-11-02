using System.Security.Claims;
using CDC.Auth.Entra;
using CDC.Auth.Entra.Claims;
using CDC.Auth.Entra.Events;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CDC.Web.Features.Account;

/// <summary>
/// Resolves an Entra ID-authenticated internal user against CDC.Api's <c>[dbo].[User]</c> row,
/// once per sign-in (called from <c>EntraOpenIdConnectEvents.TokenValidated</c>, before the local
/// cookie is written). Adds <see cref="ExternalUserClaimTypes.InternalUserId"/> and
/// <see cref="ExternalUserClaimTypes.FullName"/> so the rest of the app - including the page
/// header - never needs to call CDC.Api again to know who the user is. A successful sign-in also ends any external
/// (CIDM) session in the browser, so every later page authenticates with Entra ID.
/// </summary>
/// <param name="apiClient">Calls CDC.Api's internal-user resolve endpoint.</param>
/// <param name="logger">Logs resolution failures so a sign-in landing on /Landing/Error is diagnosable.</param>
/// <param name="httpContextAccessor">Reaches the sign-in request to end the external session; none is ended without one.</param>
public sealed partial class InternalUserResolver(
    IApiClient apiClient,
    ILogger<InternalUserResolver> logger,
    IHttpContextAccessor? httpContextAccessor = null) : IEntraInternalUserResolver
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
            LogInternalSignInDeniedNoObjectId();
            return EntraInternalUserResolution.Deny("/Account/NotPermitted");
        }

        LogInternalSignInObjectId(entraSsoId);

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
            LogInternalSignInDeniedNoAccountName(entraSsoId);
            return EntraInternalUserResolution.Deny("/Account/NotPermitted");
        }

        // Standard OIDC 'name' claim (granted by the 'profile' scope) - used only as the FullName
        // of a [dbo].[User] row created for a first-time user; ignored once a row is matched, since
        // FullName then comes from that row instead.
        var fullName = principal.FindFirst(ClaimTypes.Name)?.Value ?? principal.FindFirst("name")?.Value ?? string.Empty;

        var request = new ResolveInternalUserRequestDto
        {
            SsoUserIdInt = entraSsoId,
            UserName = userName,
            FullName = fullName
        };

        ResolveInternalUserResult result;
        try
        {
            result = await apiClient.ResolveInternalUserAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            // HttpRequestException: network/DNS failure, connection refused, or a non-success status
            // code once the resilience handler's retries are exhausted. TaskCanceledException: the
            // resilience handler's own request timeout expired. NotSupportedException: the response
            // was not valid JSON for the expected DTO. Without this, a CDC.Api connectivity problem
            // during sign-in produces zero diagnostic trace - it just lands on /Landing/Error.
            LogInternalUserResolutionFailed(entraSsoId, exception);
            return EntraInternalUserResolution.Deny("/Landing/Error");
        }

        if (result.Outcome is not (ResolveInternalUserOutcome.Success or ResolveInternalUserOutcome.NotPermitted))
        {
            LogInternalUserResolutionRejected(entraSsoId, result.Outcome);
        }

        var resolution = result switch
        {
            { Outcome: ResolveInternalUserOutcome.Success, User: not null } => EntraInternalUserResolution.Allow(
                new Dictionary<string, string>
                {
                    [ExternalUserClaimTypes.InternalUserId] = result.User.Id.ToString(),
                    [ExternalUserClaimTypes.FullName] = result.User.FullName,
                    [ExternalUserClaimTypes.AuthenticationProvider] = EntraAuthenticationDefaults.AuthenticationScheme,
                    // InternalRoleClaimsTransformation turns these two flags into role claims on every request.
                    [ExternalUserClaimTypes.IsProfileEditor] = result.User.IsProfileEditor.ToString(),
                    [ExternalUserClaimTypes.IsPolicyProfileUser] = result.User.IsPolicyProfileUser.ToString()
                }),
            { Outcome: ResolveInternalUserOutcome.NotPermitted } => EntraInternalUserResolution.Deny("/Account/NotPermitted"),
            _ => EntraInternalUserResolution.Deny("/Landing/Error")
        };

        if (resolution.IsAllowed)
        {
            await OtherSignInSession.EndAsync(httpContextAccessor, CookieAuthenticationDefaults.AuthenticationScheme);
        }

        return resolution;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Entra sign-in received for object id (oid) SsoUserIdInt={SsoUserIdInt}")]
    private partial void LogInternalSignInObjectId(Guid ssoUserIdInt);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Entra sign-in denied before calling CDC.Api: the token has no usable object id (oid) claim")]
    private partial void LogInternalSignInDeniedNoObjectId();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Entra sign-in denied before calling CDC.Api (SsoUserIdInt={SsoUserIdInt}): the token has no onprem_samaccountname claim - a cloud-only or guest account, or the optional claim is not configured on the app registration, so no user row can be matched or created")]
    private partial void LogInternalSignInDeniedNoAccountName(Guid ssoUserIdInt);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to resolve internal user (SsoUserIdInt={SsoUserIdInt}) against CDC.Api")]
    private partial void LogInternalUserResolutionFailed(Guid ssoUserIdInt, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "CDC.Api rejected internal user resolution (SsoUserIdInt={SsoUserIdInt}) with outcome {Outcome}")]
    private partial void LogInternalUserResolutionRejected(Guid ssoUserIdInt, ResolveInternalUserOutcome outcome);
}
