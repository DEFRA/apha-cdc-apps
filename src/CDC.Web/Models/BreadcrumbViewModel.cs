namespace CDC.Web.Models;

/// Represents the "Home > PageName" breadcrumb trail for a static page, or
/// "Home > ParentPageName > PageName" when <see cref="ParentPageName"/> is supplied (e.g. the
/// "History for X" view nested under "Help Using D2R2"/"Static reports").
public sealed record BreadcrumbViewModel(string PageName, string? ParentPageName = null, string? ParentUrl = null);
