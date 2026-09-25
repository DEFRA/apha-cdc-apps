namespace CDC.Web.Pages.Reports;

public class GeneralModel : BreadcrumbPageModelBase
{
    public GeneralModel() : base("General reports")
    {
    }

    public void OnGet()
    {
        // Page renders static content only; no data to load.
    }
}
