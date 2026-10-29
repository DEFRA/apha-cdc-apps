using CDC.Web.Authorization.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Minimal placeholder page for publishing a profile publicly.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.IsProfileEditor)]
public class PublishPublicModel : PageModel
{
    public void OnGet()
    {
    }
}
