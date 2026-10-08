using System.Diagnostics;
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

    // Requires authentication - the app's fallback authorization policy resolves to Entra ID's
    // cookie scheme (this app is predominantly used by internal/Entra-authenticated staff), so no
    // explicit scheme needs naming here, unlike External() below.
    public IActionResult Internal()
    {
        return View();
    }

    // Requires authentication against CIDM specifically, not the app's fallback policy (which
    // resolves to Entra ID's cookie scheme) - only reachable after a successful CIDM sign-in.
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
