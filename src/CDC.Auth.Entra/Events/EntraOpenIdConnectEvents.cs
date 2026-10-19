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
    // Claims the app reads after sign-in, on top of the common identity claims.
    private static readonly IReadOnlySet<string> RetainedClaimTypes = OpenIdConnectEventHelpers.RetainedClaims(
        EntraClaimTypes.ObjectId,
        EntraClaimTypes.ObjectIdLongClaimUri,
        "sid",
        "displayname",
        "preferred_username");

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
            (resolver, principal, cancellationToken) => resolver.ResolveAsync(principal, cancellationToken),
            RetainedClaimTypes);
    }

    /// <inheritdoc />
    public override Task TicketReceived(TicketReceivedContext context) =>
        OpenIdConnectEventHelpers.RemoveAccessTokenAsync(context);

    /// <inheritdoc />
    public override Task RemoteFailure(RemoteFailureContext context) =>
        OpenIdConnectEventHelpers.HandleRemoteFailureAsync(context, LogAuthenticationFailed);

    [LoggerMessage(Level = LogLevel.Error, Message = "Entra ID authentication failed")]
    private partial void LogAuthenticationFailed(Exception? exception);
}
