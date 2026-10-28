using CDC.Web.Authorization.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Minimal placeholder page for cloning a new scenario.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.IsProfileEditor)]
public class CloneNewScenarioModel : PageModel
{
    public void OnGet()
    {
    }
}
