using CDC.Web.Authorization.Constants;
using Microsoft.AspNetCore.Authorization;

namespace CDC.Web.Pages.DiseaseProfiles;

[Authorize(Policy = AuthorizationPolicies.CanCreateProfile)]
public class CreateModel : BreadcrumbPageModelBase
{
    public CreateModel() : base("Create profile")
    {
    }

    public void OnGet()
    {
        // Page renders static content only; no data to load.
    }
}
