using CDC.Auth.Cidm;
using CDC.Auth.Entra;
using CDC.Web.Features.LoadTesting;
using CDC.Web.Models;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Features.Account;

// loadTestSignIn is registered only while LoadTesting is switched on; while it is active, Login and LoginInternal send the user to the
// Load test login page, which signs in the user whose email address is entered, instead of to the identity provider.
public class AccountController(IAntiforgery antiforgery, ILoadTestSignIn? loadTestSignIn = null) : Controller
{
    private const string LandingController = "Landing";
    private const string ExternalHome = "External";
    private const string InternalHome = "Internal";
    private const string IdTokenName = "id_token";

    // The "kind" the Load test login page is asked for: which of the landing page's "Continue as" buttons the user pressed.
    private const string InternalKind = "internal";
    private const string ExternalKind = "external";

    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        if (await RedirectForExistingSessionAsync(UserLookupKind.External) is { } existingSession)
        {
            return existingSession;
        }

        if (loadTestSignIn is { IsActive: true })
        {
            return RedirectToAction(nameof(LoadTestLogin), new { kind = ExternalKind, returnUrl });
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
        if (await RedirectForExistingSessionAsync(UserLookupKind.Internal) is { } existingSession)
        {
            return existingSession;
        }

        if (loadTestSignIn is { IsActive: true })
        {
            return RedirectToAction(nameof(LoadTestLogin), new { kind = InternalKind, returnUrl });
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

    // Where Login and LoginInternal send a user while LoadTesting is active: the page that asks which user to sign in as. "kind" is the
    // journey chosen on the landing page. With LoadTesting off, or for any other kind, there is no such page.
    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> LoadTestLogin(string? kind, string? returnUrl = null)
    {
        if (ActiveLoadTestSignIn is null || !TryParseKind(kind, out var lookupKind))
        {
            return NotFound();
        }

        if (await RedirectForExistingSessionAsync(lookupKind) is { } existingSession)
        {
            return existingSession;
        }

        return View(new LoadTestLoginViewModel
        {
            Kind = KindName(lookupKind),
            ReturnUrl = Url.IsLocalUrl(returnUrl) ? returnUrl : null
        });
    }

    // Signs in the user whose email address was entered, then goes where the identity provider's callback would have: back to the page asked
    // for, else the user's home page. From then on the user is authorised from that user's own row, exactly as after a real sign-in.
    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> LoadTestLogin(LoadTestLoginViewModel model)
    {
        // Checked here, not by attribute: any antiforgery attribute makes CodeQL flag every [HttpPost] in the solution, CDC.Api's included.
        if (!await antiforgery.IsRequestValidAsync(HttpContext))
        {
            return BadRequest();
        }

        if (ActiveLoadTestSignIn is not { } signIn || !TryParseKind(model.Kind, out var lookupKind))
        {
            return NotFound();
        }

        if (await RedirectForExistingSessionAsync(lookupKind) is { } existingSession)
        {
            return existingSession;
        }

        // The page is shown again from the model, not from what was posted, so the journey and return page it carries on are the checked ones.
        ModelState.Clear();
        model.Kind = KindName(lookupKind);
        model.ReturnUrl = Url.IsLocalUrl(model.ReturnUrl) ? model.ReturnUrl : null;

        var emailAddress = model.EmailAddress?.Trim();
        if (string.IsNullOrEmpty(emailAddress))
        {
            return LoadTestLoginFailed(model, "Enter an email address");
        }

        var outcome = await signIn.SignInAsync(HttpContext, lookupKind, emailAddress);
        if (outcome != LoadTestSignInOutcome.SignedIn)
        {
            return LoadTestLoginFailed(model, LoadTestLoginMessage(outcome, lookupKind));
        }

        return LocalRedirect(model.ReturnUrl ?? Url.Action(HomeAction(lookupKind), LandingController)!);
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

    // The LoadTesting sign-in, or null when LoadTesting is off, in which case the Load test login page does not exist.
    private ILoadTestSignIn? ActiveLoadTestSignIn => loadTestSignIn is { IsActive: true } ? loadTestSignIn : null;

    private ViewResult LoadTestLoginFailed(LoadTestLoginViewModel model, string message)
    {
        ModelState.AddModelError(nameof(LoadTestLoginViewModel.EmailAddress), message);

        return View(model);
    }

    private static string LoadTestLoginMessage(LoadTestSignInOutcome outcome, UserLookupKind kind) => outcome switch
    {
        LoadTestSignInOutcome.NotFound => "No user has that email address",
        LoadTestSignInOutcome.NotPermitted => $"That user cannot sign in as an {KindName(kind)} user",
        LoadTestSignInOutcome.Ambiguous => "More than one user has that email address, so none can be chosen",
        LoadTestSignInOutcome.InvalidEmailAddress => "Enter an email address in the correct format, like name@example.com",
        _ => "The user could not be looked up. Try again later"
    };

    private static bool TryParseKind(string? kind, out UserLookupKind lookupKind)
    {
        if (string.Equals(kind, InternalKind, StringComparison.OrdinalIgnoreCase))
        {
            lookupKind = UserLookupKind.Internal;
            return true;
        }

        lookupKind = UserLookupKind.External;
        return string.Equals(kind, ExternalKind, StringComparison.OrdinalIgnoreCase);
    }

    private static string KindName(UserLookupKind kind) => kind == UserLookupKind.Internal ? InternalKind : ExternalKind;

    private static string HomeAction(UserLookupKind kind) => kind == UserLookupKind.Internal ? InternalHome : ExternalHome;

    // Where a user who already holds a session goes instead of signing in again, or null when there is none to respect: to their own home
    // page when the session is for this kind of user (only a CIDM session counts for an external user, and only an Entra ID one for an
    // internal user), and to AlreadySignedIn when it is for the other kind, because the internal and external sessions must not share a browser.
    private async Task<IActionResult?> RedirectForExistingSessionAsync(UserLookupKind kind)
    {
        var (ownCookieScheme, otherCookieScheme) = kind == UserLookupKind.Internal
            ? (EntraAuthenticationDefaults.CookieAuthenticationScheme, CookieAuthenticationDefaults.AuthenticationScheme)
            : (CookieAuthenticationDefaults.AuthenticationScheme, EntraAuthenticationDefaults.CookieAuthenticationScheme);

        if (await HasSessionAsync(ownCookieScheme))
        {
            return RedirectToAction(HomeAction(kind), LandingController);
        }

        if (await HasSessionAsync(otherCookieScheme))
        {
            return RedirectToAction(nameof(AlreadySignedIn));
        }

        return null;
    }

    private async Task<bool> HasSessionAsync(string cookieScheme) =>
        (await HttpContext.AuthenticateAsync(cookieScheme)).Succeeded;
}
