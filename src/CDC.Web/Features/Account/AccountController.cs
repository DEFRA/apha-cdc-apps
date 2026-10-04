using CDC.Auth.Cidm;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Features.Account;

public class AccountController : Controller
{
    [AllowAnonymous]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("External", "Landing");
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action("External", "Landing")
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
        var idToken = cookieResult?.Properties?.GetTokenValue("id_token");

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
            properties.StoreTokens([new AuthenticationToken { Name = "id_token", Value = idToken }]);
        }

        return SignOut(properties, CidmAuthenticationDefaults.AuthenticationScheme);
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
}
