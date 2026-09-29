using Microsoft.AspNetCore.Authorization;

namespace CDC.Web.Pages.HelpSupport;

[AllowAnonymous]
public class HelpUsingD2R2Model : BreadcrumbPageModelBase
{
    public HelpUsingD2R2Model() : base("Help using D2R2")
    {
    }

    public void OnGet()
    {
        // Page renders static content only; no data to load.
    }
}
