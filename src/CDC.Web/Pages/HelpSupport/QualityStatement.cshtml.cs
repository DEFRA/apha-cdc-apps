using Microsoft.AspNetCore.Authorization;

namespace CDC.Web.Pages.HelpSupport;

[AllowAnonymous]
public class QualityStatementModel : BreadcrumbPageModelBase
{
    public QualityStatementModel() : base("D2R2 Quality Statement")
    {
    }

    public void OnGet()
    {
        // Page renders static content only; no data to load.
    }
}
