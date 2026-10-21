using CDC.Auth.Entra;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace CDC.Web.Authorization;

/// <summary>
/// A policy scheme that forwards to whichever sign-in cookie the request carries, so a page open to every
/// signed-in user accepts an external (CIDM) user as well as an internal (Entra ID) one. The app's default
/// scheme is Entra's cookie, which never sees a CIDM-only session.
/// </summary>
public static class InternalOrExternalAuthenticationScheme
{
    public const string Name = "InternalOrExternal";

    /// <summary>Entra's cookie unless the request carries only the CIDM cookie, so an anonymous request is still
    /// challenged by Entra ID and a session holding both cookies is treated as internal.</summary>
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
