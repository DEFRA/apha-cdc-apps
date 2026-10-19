using CDC.Web.Authorization.Constants;
using Microsoft.AspNetCore.Authorization;

namespace CDC.Web.Pages.Reports;

[Authorize(Policy = AuthorizationPolicies.CanEditUserGuidance)]
public class QuestionsGuidanceModel : BreadcrumbPageModelBase
{
    public QuestionsGuidanceModel() : base("Questions and guidance reports")
    {
    }

    public void OnGet()
    {
        // Page renders static content only; no data to load.
    }
}
