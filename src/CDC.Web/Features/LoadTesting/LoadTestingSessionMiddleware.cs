using CDC.Auth.Entra;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace CDC.Web.Features.LoadTesting;

/// <summary>
/// Ends a session LoadTesting created once the bypass has switched itself off, so a session kept alive by activity cannot
/// outlast the window. Registered only while LoadTesting is switched on.
/// </summary>
/// <param name="next">The next middleware.</param>
/// <param name="status">Whether the bypass is active.</param>
/// <param name="logger">Logs a session being ended.</param>
internal sealed class LoadTestingSessionMiddleware(RequestDelegate next, ILoadTestingStatus status, ILogger<LoadTestingSessionMiddleware> logger)
{
    /// <summary>Lets the request through, unless it carries a LoadTesting session after the bypass ended.</summary>
    /// <param name="context">The request.</param>
    /// <returns>A task that completes when the request has been handled.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        if (status.IsActive || !context.User.HasClaim(LoadTestingDefaults.SessionClaimType, bool.TrueString))
        {
            await next(context);

            return;
        }

        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignOutAsync(EntraAuthenticationDefaults.CookieAuthenticationScheme);
        logger.SessionEnded();

        context.Response.Redirect("/");
    }
}
