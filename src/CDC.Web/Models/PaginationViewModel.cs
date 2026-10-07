namespace CDC.Web.Models;

/// <summary>
/// GOV.UK pagination state for a page list. <paramref name="BuildPageUrl"/> is supplied by the
/// page model so the shared partial doesn't need to know how each page composes its query string.
/// </summary>
public sealed record PaginationViewModel(int PageNumber, int TotalPages, Func<int, string> BuildPageUrl, string AriaLabel);
