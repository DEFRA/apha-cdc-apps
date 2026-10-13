using System.Security.Claims;
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

        await OpenIdConnectEventHelpers.ResolveOrDenyAsync<IEntraInternalUserResolver, EntraInternalUserResolution>(
            context,
            identity,
            (resolver, principal, cancellationToken) => resolver.ResolveAsync(principal, cancellationToken));
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
