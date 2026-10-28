using CDC.Web.Authorization.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Minimal placeholder page for cloning a new profile.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.IsProfileEditor)]
public class CloneNewProfileModel : PageModel
{
    public void OnGet()
    {
    }
}
