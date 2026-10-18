using CDC.Web.Authorization.Constants;
using Microsoft.AspNetCore.Authorization;

namespace CDC.Web.Pages.DiseaseProfiles;

[Authorize(Policy = AuthorizationPolicies.CanViewReviewTiming)]
public class ReviewTimingsModel : BreadcrumbPageModelBase
{
    public ReviewTimingsModel() : base("Review Timings")
    {
    }

    public void OnGet()
    {
        // Page renders static content only; no data to load.
    }
}
