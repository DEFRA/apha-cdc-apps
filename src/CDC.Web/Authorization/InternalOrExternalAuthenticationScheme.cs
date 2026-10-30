using CDC.Auth.Entra;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace CDC.Web.Authorization;

/// <summary>
/// The app's default authentication scheme: a policy scheme that forwards to whichever sign-in cookie the request carries, so
/// every page authenticates an external (CIDM) user with CIDM and an internal (Entra ID) user with Entra ID. A user a page
/// refuses is shown the access-denied page, never sent to the other provider's sign-in.
/// </summary>
public static class InternalOrExternalAuthenticationScheme
{
    public const string Name = "InternalOrExternal";

    /// <summary>Entra's cookie unless the request carries only the CIDM cookie, so an anonymous request is still challenged by
    /// Entra ID. A sign-in ends the other provider's session (see <c>OtherSignInSession</c>), so a request holding both cookies
    /// predates that and is treated as internal.</summary>
    internal static string SelectCookieScheme(HttpContext context)
    {
        var cookieOptions = context.RequestServices.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>();

        var hasInternalCookie = HasCookie(context, cookieOptions, EntraAuthenticationDefaults.CookieAuthenticationScheme);
        var hasExternalCookie = HasCookie(context, cookieOptions, CookieAuthenticationDefaults.AuthenticationScheme);

        return hasExternalCookie && !hasInternalCookie
            ? CookieAuthenticationDefaults.AuthenticationScheme
            : EntraAuthenticationDefaults.CookieAuthenticationScheme;
    }

    private static bool HasCookie(HttpContext context, IOptionsMonitor<CookieAuthenticationOptions> cookieOptions, string scheme)
    {
        var cookieName = cookieOptions.Get(scheme).Cookie.Name;
        return !string.IsNullOrEmpty(cookieName) && context.Request.Cookies.ContainsKey(cookieName);
    }
}
