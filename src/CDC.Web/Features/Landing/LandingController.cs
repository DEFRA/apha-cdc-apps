using System.Diagnostics;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Features.Landing;

public class LandingController : Controller
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

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
