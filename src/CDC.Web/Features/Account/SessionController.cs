using CDC.Common.Auth;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CDC.Web.Features.Account;

/// <summary>
/// Backs the "Extend session?" and "Session expired" dialogs (<c>wwwroot/js/session-timeout.js</c>): reports how long the signed-in
/// session has left, extends it, and sends a user whose session has ended back through sign-in.
/// </summary>
/// <remarks>
/// Every action allows anonymous requests. A script asking about a session that has ended has to be told so, and the fallback
/// authorization policy would answer it with a redirect to the identity provider instead.
/// </remarks>
[AllowAnonymous]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class SessionController(IOptions<SessionTimeoutOptions> sessionTimeout, IAntiforgery antiforgery, TimeProvider timeProvider) : Controller
{
    private const string HomeController = "Landing";

    /// <summary>Reports how long the signed-in session has left. Asking never extends it (see <see cref="PassiveSessionRequestAttribute"/>).</summary>
    /// <returns>The status as JSON, with the token to extend the session with when it has not ended.</returns>
    [HttpGet]
    [PassiveSessionRequest]
    public async Task<IActionResult> Status()
    {
        var remaining = await RemainingAsync();
        if (remaining is null)
        {
            return Json(SessionStatus.Ended);
        }

        var tokens = antiforgery.GetAndStoreTokens(HttpContext);

        return Json(new SessionStatus(true, WholeSeconds(remaining.Value), tokens.RequestToken, tokens.HeaderName));
    }

    /// <summary>Extends the signed-in session for another full timeout. A session that has already ended cannot be extended.</summary>
    /// <returns>The status as JSON; 400 without a valid antiforgery token, and 401 when the session has ended.</returns>
    [HttpPost]
    public async Task<IActionResult> Extend()
    {
        // Checked here, not by attribute: any antiforgery attribute makes CodeQL flag every [HttpPost] in the solution, CDC.Api's included.
        if (!await antiforgery.IsRequestValidAsync(HttpContext))
        {
            return BadRequest();
        }

        // The request itself is the activity that renews the session (see TokenRefreshCookieEvents); this reports how long it now has.
        var remaining = await RemainingAsync();

        return remaining is null
            ? Unauthorized(SessionStatus.Ended)
            : Json(new SessionStatus(true, WholeSeconds(remaining.Value)));
    }

    /// <summary>Sends a user whose session has ended through sign-in again, then back to the page they were on.</summary>
    /// <param name="returnUrl">A path on this site to return to afterwards; the user's home page when it is missing or points elsewhere.</param>
    /// <returns>A challenge to the user's identity provider, or a redirect when they are signed in after all.</returns>
    [HttpGet]
    public IActionResult SignInAgain(string? returnUrl)
    {
        // Only a path on this site is returned to: anything else would make signing in an open redirect.
        var target = Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action("Internal", HomeController);

        // A session that was kept alive meanwhile, from another tab say, needs no sign-in.
        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(target!);
        }

        return Challenge(new AuthenticationProperties { RedirectUri = target });
    }

    private async Task<TimeSpan?> RemainingAsync()
    {
        var authentication = await HttpContext.AuthenticateAsync();
        if (!authentication.Succeeded)
        {
            return null;
        }

        var remaining = authentication.Properties?.ExpiresUtc is { } expiresUtc
            ? expiresUtc - timeProvider.GetUtcNow()
            : sessionTimeout.Value.IdleTimeout;

        return remaining > TimeSpan.Zero ? remaining : null;
    }

    private static int WholeSeconds(TimeSpan remaining) => (int)Math.Min(Math.Ceiling(remaining.TotalSeconds), int.MaxValue);
}
