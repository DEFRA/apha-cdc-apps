namespace CDC.Web.Pages.CrossProfileAdmin;

/// <summary>Slices a list into one page for the reference data and audit trail tables.</summary>
public static class ReferenceDataPaging
{
    private const int DefaultPageSize = 10;

    /// <summary>Gets the options for the "Items per page" dropdown.</summary>
    public static IReadOnlyList<string> PageSizeOptions { get; } = ["10", "15", "20", "30", "All"];

    /// <summary>
    /// Returns the requested page of <paramref name="items"/>, clamping the page number into range.
    /// An unrecognised page size falls back to the first option.
    /// </summary>
    public static (IReadOnlyList<T> Items, int PageNumber, int TotalPages) GetPage<T>(
        IReadOnlyList<T> items, int pageNumber, string? pageSize)
    {
        var size = string.Equals(pageSize, "All", StringComparison.OrdinalIgnoreCase)
            ? Math.Max(1, items.Count)
            : int.TryParse(pageSize, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var parsed) && parsed > 0
                ? parsed
                : DefaultPageSize;

        var totalPages = Math.Max(1, (int)Math.Ceiling(items.Count / (double)size));
        var page = Math.Clamp(pageNumber, 1, totalPages);

        return ([.. items.Skip((page - 1) * size).Take(size)], page, totalPages);
    }
}
