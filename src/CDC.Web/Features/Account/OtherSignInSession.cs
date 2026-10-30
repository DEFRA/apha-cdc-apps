using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace CDC.Web.Features.Account;

/// <summary>
/// Keeps a browser to one sign-in at a time: completing a sign-in with one identity provider ends the local session of the
/// other, so every later page authenticates with the provider the user signed in with last.
/// </summary>
internal static class OtherSignInSession
{
    /// <summary>Ends the local session held in <paramref name="cookieScheme"/>, the other provider's session cookie.</summary>
    /// <param name="httpContextAccessor">Reaches the sign-in request; nothing is ended when there is none.</param>
    /// <param name="cookieScheme">The other provider's session cookie scheme.</param>
    /// <returns>A task that completes once the cookie has been cleared on the response.</returns>
    public static Task EndAsync(IHttpContextAccessor? httpContextAccessor, string cookieScheme) =>
        httpContextAccessor?.HttpContext is { } context ? context.SignOutAsync(cookieScheme) : Task.CompletedTask;
}
