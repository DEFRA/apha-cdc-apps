using CDC.Web.Authorization.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Minimal placeholder page for allowing public access on a profile.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.IsProfileEditor)]
public class AllowPublicAccessModel : PageModel
{
    public void OnGet()
    {
    }
}
