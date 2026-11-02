using CDC.Auth.Cidm;
using CDC.Auth.Entra;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Features.Account;

public class AccountController : Controller
{
    private const string LandingController = "Landing";
    private const string IdTokenName = "id_token";

    [AllowAnonymous]
    public async Task<IActionResult> Login()
    {
        // Only a CIDM session counts: an internal (Entra ID) one does not make the user an external user.
        if (await HasSessionAsync(CookieAuthenticationDefaults.AuthenticationScheme))
        {
            return RedirectToAction("External", LandingController);
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action("External", LandingController)
        };

        return Challenge(properties, CidmAuthenticationDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        // Capture id_token_hint BEFORE clearing the cookie. OpenIdConnectHandler.HandleSignOutAsync
        // only reads id_token_hint from the AuthenticationProperties passed to SignOut() (empty here)
        // or, failing that, falls back to re-authenticating against the cookie scheme - which finds
        // nothing once the cookie below has already been cleared. Without id_token_hint, CIDM (Azure AD
        // B2C) ignores post_logout_redirect_uri entirely and shows its own default sign-out page instead
        // of redirecting back to /Account/SignedOut.
        var cookieResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var idToken = cookieResult?.Properties?.GetTokenValue(IdTokenName);

        // The Cookie scheme's SignOutAsync also honors AuthenticationProperties.RedirectUri and would
        // otherwise race/short-circuit the OIDC scheme's sign-out event that renders CIDM's logout form,
        // so the two schemes must be signed out separately with only the OIDC one carrying a redirect.
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(SignedOut))
        };
        if (!string.IsNullOrEmpty(idToken))
        {
            properties.StoreTokens([new AuthenticationToken { Name = IdTokenName, Value = idToken }]);
        }

        return SignOut(properties, CidmAuthenticationDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    public async Task<IActionResult> LoginInternal()
    {
        // Only an Entra ID session counts: an external (CIDM) one does not make the user an internal user.
        if (await HasSessionAsync(EntraAuthenticationDefaults.CookieAuthenticationScheme))
        {
            return RedirectToAction("Internal", LandingController);
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action("Internal", LandingController)
        };

        return Challenge(properties, EntraAuthenticationDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    public async Task<IActionResult> LogoutInternal()
    {
        // Same id_token_hint-before-clearing-cookie subtlety as Logout() above - OpenIdConnectHandler's
        // sign-out only reads id_token_hint off AuthenticationProperties or, failing that, the cookie
        // scheme, which finds nothing once that cookie has already been cleared.
        var cookieResult = await HttpContext.AuthenticateAsync(EntraAuthenticationDefaults.CookieAuthenticationScheme);
        var idToken = cookieResult?.Properties?.GetTokenValue(IdTokenName);

        await HttpContext.SignOutAsync(EntraAuthenticationDefaults.CookieAuthenticationScheme);

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(SignedOut))
        };
        if (!string.IsNullOrEmpty(idToken))
        {
            properties.StoreTokens([new AuthenticationToken { Name = IdTokenName, Value = idToken }]);
        }

        return SignOut(properties, EntraAuthenticationDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    public IActionResult SignedOut()
    {
        return View();
    }

    // Reached when ICidmExternalUserResolver denies sign-in (the email claim matched an existing
    // non-external user) - no local cookie exists at this point, since TokenValidated redirects
    // here before the OIDC handler ever signs the principal into the cookie scheme.
    [AllowAnonymous]
    public IActionResult NotPermitted()
    {
        return View();
    }

    // Where the cookie schemes (default AccessDeniedPath) send a signed-in user whose role a page's policy refuses.
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;

        return View();
    }

    private async Task<bool> HasSessionAsync(string cookieScheme) =>
        (await HttpContext.AuthenticateAsync(cookieScheme)).Succeeded;
}
