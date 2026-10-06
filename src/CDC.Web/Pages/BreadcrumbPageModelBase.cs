using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages;

/// Base page model for static placeholder pages that need a breadcrumb trail.
public abstract class BreadcrumbPageModelBase : PageModel
{
    protected BreadcrumbPageModelBase(string pageName)
    {
        Breadcrumb = new BreadcrumbViewModel(pageName);
    }

    // Settable so pages whose breadcrumb depends on a querystring (e.g. StaticReports?UserManual=1)
    // can update it once the request is known, in OnGet.
    public BreadcrumbViewModel Breadcrumb { get; protected set; }
}
