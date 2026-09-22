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

    public BreadcrumbViewModel Breadcrumb { get; }
}
