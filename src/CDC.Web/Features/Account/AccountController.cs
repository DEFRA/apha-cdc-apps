using CDC.Auth.Cidm;
using CDC.Auth.Entra;
using CDC.Web.Features.LoadTesting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Features.Account;

// loadTestSignIn is registered only while LoadTesting is switched on; while it is active, sign-in skips the identity provider.
public class AccountController(ILoadTestSignIn? loadTestSignIn = null) : Controller
{
    private const string LandingController = "Landing";
    private const string ExternalHome = "External";
    private const string InternalHome = "Internal";
    private const string IdTokenName = "id_token";

    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        // Only a CIDM session counts: an internal (Entra ID) one does not make the user an external user.
        if (await HasSessionAsync(CookieAuthenticationDefaults.AuthenticationScheme))
        {
            return RedirectToAction(ExternalHome, LandingController);
        }

        // The internal and external sessions must not share a browser, so an internal user is stopped here.
        if (await HasSessionAsync(EntraAuthenticationDefaults.CookieAuthenticationScheme))
        {
            return RedirectToAction(nameof(AlreadySignedIn));
        }

        if (loadTestSignIn is { IsActive: true })
        {
            return LoadTestRedirect(await loadTestSignIn.SignInExternalAsync(HttpContext), returnUrl, ExternalHome);
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(ExternalHome, LandingController)
        };

        return Challenge(properties, CidmAuthenticationDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        // There is no identity provider session to end while LoadTesting is active, only the local one.
        if (loadTestSignIn is { IsActive: true })
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction(nameof(SignedOut));
        }

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
    public async Task<IActionResult> LoginInternal(string? returnUrl = null)
    {
        // Only an Entra ID session counts: an external (CIDM) one does not make the user an internal user.
        if (await HasSessionAsync(EntraAuthenticationDefaults.CookieAuthenticationScheme))
        {
            return RedirectToAction(InternalHome, LandingController);
        }

        // The internal and external sessions must not share a browser, so an external user is stopped here.
        if (await HasSessionAsync(CookieAuthenticationDefaults.AuthenticationScheme))
        {
            return RedirectToAction(nameof(AlreadySignedIn));
        }

        if (loadTestSignIn is { IsActive: true })
        {
            return LoadTestRedirect(await loadTestSignIn.SignInInternalAsync(HttpContext), returnUrl, InternalHome);
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(InternalHome, LandingController)
        };

        return Challenge(properties, EntraAuthenticationDefaults.AuthenticationScheme);
    }

    [AllowAnonymous]
    public async Task<IActionResult> LogoutInternal()
    {
        if (loadTestSignIn is { IsActive: true })
        {
            await HttpContext.SignOutAsync(EntraAuthenticationDefaults.CookieAuthenticationScheme);

            return RedirectToAction(nameof(SignedOut));
        }

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

    // Where Login/LoginInternal send a user who already holds the other kind of session. Which session they hold is read
    // from the cookies here, never from the request, so the page cannot be made to claim a session the user does not have.
    [AllowAnonymous]
    public async Task<IActionResult> AlreadySignedIn()
    {
        if (await HasSessionAsync(EntraAuthenticationDefaults.CookieAuthenticationScheme))
        {
            return View(new AlreadySignedInViewModel("internal", InternalHome));
        }

        if (await HasSessionAsync(CookieAuthenticationDefaults.AuthenticationScheme))
        {
            return View(new AlreadySignedInViewModel("external", ExternalHome));
        }

        return RedirectToAction("Index", LandingController);
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

    // After a LoadTesting sign-in, goes where the identity provider's callback would have: back to the page asked for, else the user's home page.
    private IActionResult LoadTestRedirect(LoadTestSignInResult result, string? returnUrl, string homeAction)
    {
        if (!result.Succeeded)
        {
            return Redirect(result.DenialRedirectPath ?? "/");
        }

        var target = Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action(homeAction, LandingController);

        return LocalRedirect(target!);
    }

    private async Task<bool> HasSessionAsync(string cookieScheme) =>
        (await HttpContext.AuthenticateAsync(cookieScheme)).Succeeded;
}
