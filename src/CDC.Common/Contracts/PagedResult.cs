namespace CDC.Common.Contracts;

/// <summary>
/// A single page of results plus enough information to render paging controls. Shared by
/// CDC.Api's paginated responses and CDC.Web's consumption of them.
/// </summary>
/// <typeparam name="T">Type of each item in <see cref="Items"/>.</typeparam>
public sealed record PagedResult<T>
{
    /// <summary>Gets the items on this page.</summary>
    public required IReadOnlyList<T> Items { get; init; }

    /// <summary>Gets the current 1-based page number.</summary>
    public required int PageNumber { get; init; }

    /// <summary>Gets the number of items per page. 0 means every matching record was returned on
    /// a single page (an "All" items-per-page option).</summary>
    public required int PageSize { get; init; }

    /// <summary>Gets the total number of records matching the request, across every page.</summary>
    public required int TotalRecords { get; init; }

    /// <summary>Gets the total number of pages, always at least 1.</summary>
    public int TotalPages => PageSize > 0 ? Math.Max(1, (int)Math.Ceiling(TotalRecords / (double)PageSize)) : 1;
}
