using System.Globalization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CDC.Common.Auth;

/// <summary>
/// Refreshes tokens ahead of access token expiry on the app's own session cookie, so a request never
/// proceeds with a token known to be expired or about to expire. On refresh failure (network error, the
/// refresh_token itself has exceeded its lifetime, or the user's account was disabled/removed at the
/// identity provider) the principal is rejected, forcing a fresh interactive sign-in on the next request -
/// which re-validates against the provider on a schedule rather than trusting the session cookie for its
/// full lifetime. Each identity provider supplies only its own refresh service, callback path and timing.
/// </summary>
/// <remarks>
/// The same events make the cookie's <see cref="CookieAuthenticationOptions.ExpireTimeSpan"/> an idle timeout: a request is
/// activity and renews the session for another full timeout, except a request to an endpoint marked
/// <see cref="PassiveSessionRequestAttribute"/>, which leaves the session exactly as it found it.
/// </remarks>
public abstract partial class TokenRefreshCookieEvents : CookieAuthenticationEvents
{
    private const string ExpiresAtTokenName = "expires_at";
    private const string RefreshTokenName = "refresh_token";
    private const string AccessTokenName = "access_token";
    private const string IdTokenName = "id_token";

    // A session is renewed once this fraction of its timeout has passed since it was last renewed (a minute, for a 20-minute timeout).
    private const int RenewalDivisor = 20;

    private readonly ITokenRefreshService _refreshService;
    private readonly string _providerName;
    private readonly string _callbackPath;
    private readonly TimeSpan _refreshBeforeExpiry;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    /// <summary>Creates the event handler with its refresh, timing and logging dependencies.</summary>
    /// <param name="refreshService">Exchanges the refresh_token for a new token set.</param>
    /// <param name="providerName">Human-readable provider name, used in log messages.</param>
    /// <param name="callbackPath">The provider's OIDC callback path, which forms the refresh redirect_uri.</param>
    /// <param name="refreshBeforeExpiry">How long before access token expiry to refresh.</param>
    /// <param name="timeProvider">Source of the current time.</param>
    /// <param name="logger">Logs refresh failures.</param>
    protected TokenRefreshCookieEvents(
        ITokenRefreshService refreshService,
        string providerName,
        string callbackPath,
        TimeSpan refreshBeforeExpiry,
        TimeProvider timeProvider,
        ILogger logger)
    {
        _refreshService = refreshService;
        _providerName = providerName;
        _callbackPath = callbackPath;
        _refreshBeforeExpiry = refreshBeforeExpiry;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    /// <remarks>
    /// The built-in rule re-issues the cookie only once half its lifetime has passed, so a session that went quiet could end after as
    /// little as half the timeout. Any request counts as activity instead, as it did in the legacy ASP.NET session, and the cookie is
    /// re-issued - which re-sends every token it holds - at most once per twentieth of the timeout, so the timeout is exact to
    /// within that. A renewed session is re-based on the configured timeout, so a change to it also applies to sessions that are
    /// already open.
    /// </remarks>
    public override Task CheckSlidingExpiration(CookieSlidingExpirationContext context)
    {
        var renew = !IsPassiveRequest(context.HttpContext) && context.ElapsedTime >= context.Options.ExpireTimeSpan / RenewalDivisor;

        if (renew)
        {
            var now = _timeProvider.GetUtcNow();
            context.Properties.IssuedUtc = now;
            context.Properties.ExpiresUtc = now + context.Options.ExpireTimeSpan;
        }

        context.ShouldRenew = renew;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        if (IsPassiveRequest(context.HttpContext))
        {
            // Exchanging the refresh token would mean re-issuing the cookie to keep the new one, and so extending the session.
            return;
        }

        var expiresAtRaw = context.Properties.GetTokenValue(ExpiresAtTokenName);
        var refreshToken = context.Properties.GetTokenValue(RefreshTokenName);

        if (expiresAtRaw is null || refreshToken is null ||
            !DateTimeOffset.TryParse(expiresAtRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var expiresAt))
        {
            // No refreshable token metadata on this cookie - leave the existing principal as-is rather
            // than forcing a sign-out over something this feature doesn't apply to.
            return;
        }

        if (expiresAt - _timeProvider.GetUtcNow() > _refreshBeforeExpiry)
        {
            return;
        }

        var redirectUri = BuildRedirectUri(context.Request);
        var result = await _refreshService.RefreshAsync(refreshToken, redirectUri, context.HttpContext.RequestAborted);

        if (!result.Succeeded)
        {
            LogRefreshFailed(_providerName);
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(context.Scheme.Name);
            return;
        }

        context.Properties.UpdateTokenValue(AccessTokenName, result.AccessToken!);
        if (result.IdToken is not null)
        {
            context.Properties.UpdateTokenValue(IdTokenName, result.IdToken);
        }

        context.Properties.UpdateTokenValue(RefreshTokenName, result.RefreshToken ?? refreshToken);
        context.Properties.UpdateTokenValue(ExpiresAtTokenName, result.ExpiresAt!.Value.ToString("o", CultureInfo.InvariantCulture));

        context.ShouldRenew = true;
    }

    private static bool IsPassiveRequest(HttpContext httpContext) =>
        httpContext.GetEndpoint()?.Metadata.GetMetadata<PassiveSessionRequestAttribute>() is not null;

    private string BuildRedirectUri(HttpRequest request) =>
        $"{request.Scheme}://{request.Host}{_callbackPath}";

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Provider} token refresh failed for the current session - rejecting the principal")]
    private partial void LogRefreshFailed(string provider);
}
