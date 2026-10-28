using CDC.Web.Authorization.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Minimal placeholder page for creating a new draft version.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.IsProfileEditor)]
public class CreateNewDraftVersionModel : PageModel
{
    public void OnGet()
    {
    }
}
