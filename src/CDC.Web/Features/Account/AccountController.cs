using CDC.Auth.Cidm;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Features.Account;

public class AccountController : Controller
{
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

    public async Task<IActionResult> Logout()
    {
        // The Cookie scheme's SignOutAsync also honors AuthenticationProperties.RedirectUri and would
        // otherwise race/short-circuit the OIDC scheme's sign-out event that renders CIDM's logout form,
        // so the two schemes must be signed out separately with only the OIDC one carrying a redirect.
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(SignedOut))
        };

        return SignOut(properties, CidmAuthenticationDefaults.AuthenticationScheme);
    }

    public IActionResult SignedOut()
    {
        return View();
    }
}
