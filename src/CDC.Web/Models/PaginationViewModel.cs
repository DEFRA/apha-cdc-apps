namespace CDC.Web.Models;

/// <summary>One entry in a pagination list: a page number, or an ellipsis standing for pages that are not listed.</summary>
/// <param name="Number">The page number, or <see langword="null"/> for an ellipsis.</param>
public readonly record struct PaginationItem(int? Number)
{
    /// <summary>Gets a value indicating whether this entry is an ellipsis rather than a page.</summary>
    public bool IsEllipsis => Number is null;
}

/// <summary>
/// Data needed to render the shared <c>_Pagination</c> partial: the GOV.UK Design System pagination for moving between
/// pages of items. It renders nothing when there is only one page.
/// </summary>
public sealed record PaginationViewModel
{
    /// <summary>Gets the page being shown, counting from 1.</summary>
    public required int CurrentPage { get; init; }

    /// <summary>Gets how many pages there are.</summary>
    public required int TotalPages { get; init; }

    /// <summary>Gets what builds the URL of a page, which may be <see langword="null"/> when the page has none.</summary>
    public required Func<int, string?> PageUrl { get; init; }

    /// <summary>Gets the accessible name of the navigation landmark, which must differ from any other navigation on the page.</summary>
    public string LandmarkLabel { get; init; } = "Pagination";

    /// <summary>Gets a value indicating whether each page link carries its page number in <c>data-app-page</c>, for a script
    /// that follows the link itself instead of the browser.</summary>
    public bool AddPageData { get; init; }

    /// <summary>Gets the entries to list, as <see cref="BuildItems"/> works them out.</summary>
    public IReadOnlyList<PaginationItem> Items => BuildItems(CurrentPage, TotalPages);

    /// <summary>
    /// Works out which pages to list, as the Design System describes: the first and last pages and the current page with
    /// the pages either side of it, with an ellipsis for pages left out. One page left out is listed instead, as an ellipsis
    /// there would stand for no more than it takes to list it.
    /// </summary>
    /// <param name="currentPage">The page being shown, counting from 1.</param>
    /// <param name="totalPages">How many pages there are.</param>
    /// <returns>The entries to list, in order.</returns>
    public static IReadOnlyList<PaginationItem> BuildItems(int currentPage, int totalPages)
    {
        if (totalPages < 1)
        {
            return [];
        }

        var current = Math.Clamp(currentPage, 1, totalPages);
        var listed = new SortedSet<int> { 1, current - 1, current, current + 1, totalPages };
        listed.RemoveWhere(page => page < 1 || page > totalPages);

        var items = new List<PaginationItem>();
        var previous = 0;

        foreach (var page in listed)
        {
            var left = page - previous - 1;

            if (left == 1)
            {
                items.Add(new PaginationItem(previous + 1));
            }
            else if (left > 1)
            {
                items.Add(new PaginationItem(null));
            }

            items.Add(new PaginationItem(page));
            previous = page;
        }

        return items;
    }

    /// <summary>Adds the page number to a page title when there is more than one page, as the Design System asks, so a screen
    /// reader user can tell they have moved to another page.</summary>
    /// <param name="title">The page title.</param>
    /// <param name="currentPage">The page being shown, counting from 1.</param>
    /// <param name="totalPages">How many pages there are.</param>
    /// <returns>For example "General reports (page 2 of 4)", or <paramref name="title"/> unchanged when there is one page.</returns>
    public static string TitleWithPage(string title, int currentPage, int totalPages) =>
        totalPages > 1 ? $"{title} (page {currentPage} of {totalPages})" : title;
}
