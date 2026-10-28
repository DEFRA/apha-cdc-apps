using CDC.Web.Authorization.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Minimal placeholder page for DefraNet-only profile publishing.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.IsProfileEditor)]
public class PublishDefranetOnlyModel : PageModel
{
    public void OnGet()
    {
    }
}
