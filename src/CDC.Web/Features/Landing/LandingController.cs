using System.Diagnostics;
using CDC.Auth.Cidm;
using CDC.Web.Features.Account;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Features.Landing;

public class LandingController(IApiClient apiClient) : Controller
{
    [AllowAnonymous]
    public IActionResult Index()
    {
        return View();
    }

    // Requires authentication - the app's fallback authorization policy accepts whichever sign-in cookie the request carries, so
    // no explicit scheme is named here. Every "D2R2 Home" link points here, so an external user is sent on to their own home page.
    public IActionResult Internal()
    {
        if (User.HasClaim(ExternalUserClaimTypes.AuthenticationProvider, CidmAuthenticationDefaults.AuthenticationScheme))
        {
            return RedirectToAction(nameof(External));
        }

        return View();
    }

    // Requires authentication against CIDM specifically, not the fallback policy (which accepts either provider's cookie)
    // - only reachable after a successful CIDM sign-in.
    [Authorize(AuthenticationSchemes = CookieAuthenticationDefaults.AuthenticationScheme)]
    public IActionResult External()
    {
        return View();
    }

    // Diagnostic endpoint proving Web -> Api connectivity; useful as a smoke-test in any environment.
    [AllowAnonymous]
    public async Task<IActionResult> ApiStatus(CancellationToken cancellationToken)
    {
        var health = await apiClient.GetHealthAsync(cancellationToken);
        return Json(health);
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
