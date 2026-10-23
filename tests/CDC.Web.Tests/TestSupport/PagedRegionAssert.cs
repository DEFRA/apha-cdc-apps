namespace CDC.Web.Tests.TestSupport;

/// <summary>Assertions for the region of a page that its pagination links reload in place (see paged-region.js).</summary>
internal static class PagedRegionAssert
{
    /// <summary>Asserts that the page marks the element with the given id as a paged region.</summary>
    /// <param name="body">The HTML of the page.</param>
    /// <param name="regionId">The id the script looks the region up by, in the page it loads as well as in the page it replaces.</param>
    /// <returns>Where the region starts in <paramref name="body"/>.</returns>
    public static int IsMarked(string body, string regionId)
    {
        var start = body.IndexOf($"id=\"{regionId}\"", StringComparison.Ordinal);

        Assert.True(start >= 0, $"The page has no element with the id '{regionId}'.");
        Assert.Contains("data-app-paged-region", body[start..body.IndexOf('>', start)], StringComparison.Ordinal);

        return start;
    }

    /// <summary>Asserts that the page marks a paged region, and that its pagination comes after the start of it.</summary>
    /// <param name="body">The HTML of the page.</param>
    /// <param name="regionId">The id the script looks the region up by.</param>
    public static void ContainsPagination(string body, string regionId)
    {
        var start = IsMarked(body, regionId);

        Assert.True(
            body.IndexOf("<nav class=\"govuk-pagination\"", start, StringComparison.Ordinal) > start,
            $"The pagination does not come after the start of '{regionId}'.");
    }
}
