using CDC.Web.Models;

namespace CDC.Web.Pages.HelpSupport;

/// <summary>Sorts static report lists by a header column; shared by the general reports and static reports pages.</summary>
internal static class StaticReportSorter
{
    public static IReadOnlyList<StaticReportListItemDto> Sort(
        IReadOnlyList<StaticReportListItemDto> reports, StaticReportSortColumn column, bool descending)
    {
        // A report still in effect has no end date, so it sorts after every superseded one.
        var sorted = column switch
        {
            StaticReportSortColumn.Version => Order(reports, report => report.VersionMajor, descending),
            StaticReportSortColumn.Public => Order(reports, report => report.IsPublic, descending),
            StaticReportSortColumn.EffectiveDates => Order(reports, report => (report.EffectiveDateFrom, report.EffectiveDateTo ?? DateTime.MaxValue), descending),
            _ => Order(reports, report => report.Title, descending, StringComparer.OrdinalIgnoreCase)
        };

        // Title breaks ties between equal values so the order is stable between requests.
        return [.. sorted.ThenBy(report => report.Title, StringComparer.OrdinalIgnoreCase)];
    }

    private static IOrderedEnumerable<StaticReportListItemDto> Order<TKey>(
        IEnumerable<StaticReportListItemDto> reports, Func<StaticReportListItemDto, TKey> key, bool descending, IComparer<TKey>? comparer = null) =>
        descending ? reports.OrderByDescending(key, comparer) : reports.OrderBy(key, comparer);
}
