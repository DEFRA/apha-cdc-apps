namespace CDC.Web.Models;

/// Represents the "Home > Section Name > Page Name" breadcrumb trail for a static page.
public sealed record BreadcrumbViewModel(string SectionName, string PageName);
