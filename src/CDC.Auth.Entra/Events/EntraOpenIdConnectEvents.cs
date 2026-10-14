using System.Security.Claims;
using CDC.Auth.Entra.Claims;
using CDC.Common.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Logging;

namespace CDC.Auth.Entra.Events;

/// <summary>
/// Entra ID-specific hooks into the OpenID Connect handler: resolves the signed-in identity to the
/// application's own user record, and hides OIDC failure detail from the user.
/// </summary>
public sealed partial class EntraOpenIdConnectEvents : OpenIdConnectEvents
{
    private const string AccessTokenName = "access_token";

    // Claims the app reads after sign-in; the rest of the id_token's claims only bloat the cookie past the WAF's 10 KB cookie-header limit.
    private static readonly HashSet<string> RetainedClaimTypes = new(StringComparer.Ordinal)
    {
        ClaimTypes.NameIdentifier,
        ClaimTypes.Name,
        ClaimTypes.GivenName,
        ClaimTypes.Surname,
        ClaimTypes.Email,
        EntraClaimTypes.ObjectId,
        EntraClaimTypes.ObjectIdLongClaimUri,
        "sub",
        "sid",
        "name",
        "displayname",
        "preferred_username",
        "given_name",
        "family_name",
        "email"
    };

    private readonly ILogger<EntraOpenIdConnectEvents> _logger;

    /// <summary>Creates the event handler with its logging dependency.</summary>
    public EntraOpenIdConnectEvents(ILogger<EntraOpenIdConnectEvents> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task TokenValidated(TokenValidatedContext context)
    {
        if (context.Principal?.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        var tokenClaims = identity.Claims.ToList();

        await OpenIdConnectEventHelpers.ResolveOrDenyAsync<IEntraInternalUserResolver, EntraInternalUserResolution>(
            context,
            identity,
            (resolver, principal, cancellationToken) => resolver.ResolveAsync(principal, cancellationToken));

        if (context.Result?.Handled ?? false)
        {
            return;
        }

        // Only the claims present before resolution are trimmed, so the ones the resolver just added are kept.
        foreach (var claim in tokenClaims.Where(claim => !RetainedClaimTypes.Contains(claim.Type)))
        {
            identity.RemoveClaim(claim);
        }
    }

    /// <inheritdoc />
    public override Task TicketReceived(TicketReceivedContext context)
    {
        // Nothing uses the access token, and it is the largest avoidable part of the session cookie, which must stay under the WAF's 10 KB cookie-header limit.
        if (context.Properties is { } properties)
        {
            properties.StoreTokens(properties.GetTokens().Where(token => token.Name != AccessTokenName).ToList());
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task RemoteFailure(RemoteFailureContext context) =>
        OpenIdConnectEventHelpers.HandleRemoteFailureAsync(context, LogAuthenticationFailed);

    [LoggerMessage(Level = LogLevel.Error, Message = "Entra ID authentication failed")]
    private partial void LogAuthenticationFailed(Exception? exception);
}
