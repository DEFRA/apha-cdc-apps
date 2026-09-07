using System.Diagnostics;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Features.Landing;

public class LandingController(IApiClient apiClient) : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Internal()
    {
        return View();
    }

    public IActionResult External()
    {
        return View();
    }

    // Diagnostic endpoint proving Web -> Api connectivity; useful as a smoke-test in any environment.
    public async Task<IActionResult> ApiStatus(CancellationToken cancellationToken)
    {
        var health = await apiClient.GetHealthAsync(cancellationToken);
        return Json(health);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
