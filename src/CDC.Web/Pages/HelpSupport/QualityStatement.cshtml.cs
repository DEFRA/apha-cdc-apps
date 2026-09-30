using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.HelpSupport;

public class QualityStatementModel : BreadcrumbPageModelBase
{
    private readonly IWebHostEnvironment _environment;

    public QualityStatementModel(IWebHostEnvironment environment) : base("D2R2 Quality Statement")
    {
        _environment = environment;
    }

    public void OnGet()
    {
        // Page renders static content only; no data to load.
    }
}
