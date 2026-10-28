using CDC.Web.Authorization.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Minimal placeholder page for editing profile properties.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.IsProfileEditor)]
public class EditPropertiesModel : PageModel
{
    public void OnGet()
    {
    }
}
