using CDC.Web.Authorization.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Minimal placeholder page for deleting the current profile version.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.IsProfileEditor)]
public class DeleteCurrentVersionModel : PageModel
{
    public void OnGet()
    {
    }
}
