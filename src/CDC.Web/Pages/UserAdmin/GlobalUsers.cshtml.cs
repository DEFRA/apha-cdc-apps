namespace CDC.Web.Pages.UserAdmin;

public class GlobalUsersModel : BreadcrumbPageModelBase
{
    public GlobalUsersModel() : base("Global users")
    {
    }

    public void OnGet()
    {
        // Page renders static content only; no data to load.
    }
}
