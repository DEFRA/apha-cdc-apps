using CDC.Auth.Entra.Options;
using CDC.Common.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CDC.Auth.Entra.TokenRefresh;

/// <summary>Refreshes Entra ID tokens on the app's own session cookie ahead of access token expiry.</summary>
public sealed class EntraCookieEvents : TokenRefreshCookieEvents
{
    /// <summary>Creates the event handler with its refresh, options, time, and logging dependencies.</summary>
    public EntraCookieEvents(
        IEntraTokenRefreshService refreshService,
        IOptions<EntraOptions> entraOptions,
        TimeProvider timeProvider,
        ILogger<EntraCookieEvents> logger)
        : base(
            refreshService,
            "Entra ID",
            entraOptions.Value.CallbackPath,
            entraOptions.Value.RefreshBeforeExpiry,
            timeProvider,
            logger)
    {
    }
}
