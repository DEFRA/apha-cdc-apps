using System.Security.Claims;
using CDC.Auth.Cidm;
using CDC.Auth.Cidm.Claims;
using CDC.Auth.Cidm.Events;
using CDC.Auth.Entra;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CDC.Web.Features.Account;

/// <summary>
/// Resolves a CIDM-authenticated external user against CDC.Api's <c>[dbo].[User]</c> row, once per
/// sign-in (called from <c>CidmOpenIdConnectEvents.TokenValidated</c>, before the local cookie is
/// written). Adds <see cref="ExternalUserClaimTypes.InternalUserId"/> and
/// <see cref="ExternalUserClaimTypes.FullName"/> so the rest of the app - including the page
/// header - never needs to call CDC.Api again to know who the user is. A successful sign-in also ends any internal
/// (Entra ID) session in the browser, so every later page authenticates with CIDM.
/// </summary>
/// <param name="apiClient">Calls CDC.Api's external-user resolve endpoint.</param>
/// <param name="logger">Logs resolution failures so a sign-in landing on /Landing/Error is diagnosable.</param>
/// <param name="httpContextAccessor">Reaches the sign-in request to end the internal session; none is ended without one.</param>
public sealed partial class ExternalUserResolver(
    IApiClient apiClient,
    ILogger<ExternalUserResolver> logger,
    IHttpContextAccessor? httpContextAccessor = null) : ICidmExternalUserResolver
{
    /// <inheritdoc />
    public async Task<CidmExternalUserResolution> ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);

        // JwtSecurityTokenHandler's default inbound claim mapping remaps the JWT 'sub' claim to
        // ClaimTypes.NameIdentifier; "sub" is also checked directly in case mapping is ever turned off.
        var cidmSsoIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? principal.FindFirst("sub")?.Value;
        if (!Guid.TryParse(cidmSsoIdClaim, out var cidmSsoId))
        {
            return CidmExternalUserResolution.Deny("/Account/NotPermitted");
        }

        // Same default inbound mapping remaps the JWT 'email' claim to ClaimTypes.Email.
        var email = principal.FindFirst(ClaimTypes.Email)?.Value ?? principal.FindFirst("email")?.Value ?? string.Empty;
        var firstName = principal.FindFirst("firstName")?.Value ?? string.Empty;
        var lastName = principal.FindFirst("lastName")?.Value ?? string.Empty;

        // CidmOpenIdConnectEvents.TokenValidated maps the raw "relationships" claims before calling
        // this resolver. A user can belong to more than one organisation, so prefer whichever
        // relationship CIDM says was selected for this session (currentRelationshipId); only fall
        // back to the first relationship when that claim is absent or doesn't match any of them.
        // [dbo].[User].Organisation is NOT NULL, so this is an empty string, never null, when the
        // user has no relationships at all.
        var relationships = CidmClaimsMapper.ParseRelationships(
            principal.FindAll(CidmClaimTypes.RawRelationships).Select(claim => claim.Value));
        var currentRelationshipId = principal.FindFirst(CidmClaimTypes.CurrentRelationshipId)?.Value;
        RelationshipInfo? currentRelationship = null;
        foreach (var relationship in relationships)
        {
            if (relationship.RelationshipId == currentRelationshipId)
            {
                currentRelationship = relationship;
                break;
            }
        }
        currentRelationship ??= relationships.Count > 0 ? relationships[0] : null;
        var organisation = currentRelationship?.OrganisationName ?? string.Empty;

        var request = new ResolveExternalUserRequestDto
        {
            SsoUserIdExt = cidmSsoId,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Organisation = organisation
        };

        ResolveExternalUserResult result;
        try
        {
            result = await apiClient.ResolveExternalUserAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            // HttpRequestException: network/DNS failure, connection refused, or a non-success status
            // code once the resilience handler's retries are exhausted. TaskCanceledException: the
            // resilience handler's own request timeout expired. NotSupportedException: the response
            // was not valid JSON for the expected DTO. Without this, a CDC.Api connectivity problem
            // during sign-in produces zero diagnostic trace - it just lands on /Landing/Error.
            LogExternalUserResolutionFailed(cidmSsoId, exception);
            return CidmExternalUserResolution.Deny("/Landing/Error");
        }

        if (result.Outcome is not (ResolveExternalUserOutcome.Success or ResolveExternalUserOutcome.NotPermitted))
        {
            LogExternalUserResolutionRejected(cidmSsoId, result.Outcome);
        }

        var resolution = result switch
        {
            { Outcome: ResolveExternalUserOutcome.Success, User: not null } => CidmExternalUserResolution.Allow(
                new Dictionary<string, string>
                {
                    [ExternalUserClaimTypes.InternalUserId] = result.User.Id.ToString(),
                    [ExternalUserClaimTypes.FullName] = result.User.FullName,
                    [ExternalUserClaimTypes.AuthenticationProvider] = CidmAuthenticationDefaults.AuthenticationScheme
                }),
            { Outcome: ResolveExternalUserOutcome.NotPermitted } => CidmExternalUserResolution.Deny("/Account/NotPermitted"),
            _ => CidmExternalUserResolution.Deny("/Landing/Error")
        };

        if (resolution.IsAllowed)
        {
            await OtherSignInSession.EndAsync(httpContextAccessor, EntraAuthenticationDefaults.CookieAuthenticationScheme);
        }

        return resolution;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to resolve external user (SsoUserIdExt={SsoUserIdExt}) against CDC.Api")]
    private partial void LogExternalUserResolutionFailed(Guid ssoUserIdExt, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "CDC.Api rejected external user resolution (SsoUserIdExt={SsoUserIdExt}) with outcome {Outcome}")]
    private partial void LogExternalUserResolutionRejected(Guid ssoUserIdExt, ResolveExternalUserOutcome outcome);
}
