using System.Diagnostics;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
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

    [AllowAnonymous]
    public IActionResult Internal()
    {
        return View();
    }

    // Requires authentication (via the app's default authorization policy) - only reachable after a
    // successful CIDM sign-in.
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
