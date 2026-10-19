using CDC.Auth.Cidm.Options;
using CDC.Common.Auth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CDC.Auth.Cidm.TokenRefresh;

/// <summary>Refreshes CIDM tokens on the app's own session cookie ahead of access token expiry.</summary>
public sealed class CidmCookieEvents : TokenRefreshCookieEvents
{
    /// <summary>Creates the event handler with its refresh, options, time, and logging dependencies.</summary>
    public CidmCookieEvents(
        ICidmTokenRefreshService refreshService,
        IOptions<CidmOptions> cidmOptions,
        TimeProvider timeProvider,
        ILogger<CidmCookieEvents> logger)
        : base(
            refreshService,
            "CIDM",
            cidmOptions.Value.CallbackPath,
            cidmOptions.Value.RefreshBeforeExpiry,
            timeProvider,
            logger)
    {
    }
}
