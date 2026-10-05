using System.Globalization;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Pages.HelpSupport;

/// <summary>
/// Previous versions of one "Help using D2R2" document. Replaces the legacy
/// <c>StaticReports.aspx?StaticReportId=...</c> history mode.
/// </summary>
/// <param name="staticReportsApiService">Typed client for the static reports endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class DocumentHistoryModel(IStaticReportsApiService staticReportsApiService, ILogger<DocumentHistoryModel> logger)
    : BreadcrumbPageModelBase("Help using D2R2")
{
    private const int DefaultPageSize = 10;

    public static IReadOnlyList<int> PageSizeOptions { get; } = [10, 20, 30, 50];

    /// <summary>Gets every version of the document, most recent first unless the user chooses ascending.</summary>
    public IReadOnlyList<DocumentHistoryRow> Versions { get; private set; } = [];

    /// <summary>Gets the sort direction for the version list.</summary>
    public string SortOrder { get; private set; } = "desc";

    /// <summary>Gets or sets the current results page number.</summary>
    public int PageNumber { get; private set; } = 1;

    /// <summary>Gets or sets the number of items shown per page.</summary>
    public int PageSize { get; private set; } = DefaultPageSize;

    /// <summary>Gets the total number of pages needed to display every version.</summary>
    public int TotalPages { get; private set; } = 1;

    /// <summary>Gets the selected document title for the page heading.</summary>
    public string SelectedDocumentTitle { get; private set; } = "this manual";

    /// <summary>Gets the selected static report identifier used to keep the page state when sorting or paging.</summary>
    public Guid StaticReportId { get; private set; }

    /// <summary>Gets a value indicating whether the history could not be loaded.</summary>
    public bool HasError { get; private set; }

    public async Task OnGetAsync(Guid staticReportId, string sortOrder, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        try
        {
            StaticReportId = staticReportId;
            var history = await staticReportsApiService.GetHistoryAsync(staticReportId, cancellationToken);

            SortOrder = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase) ? "asc" : "desc";
            PageSize = ResolvePageSize(pageSize);

            var orderedVersions = SortVersions(history, SortOrder);
            var totalCount = orderedVersions.Count;
            TotalPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / PageSize));
            PageNumber = Math.Clamp(pageNumber, 1, TotalPages);

            SelectedDocumentTitle = orderedVersions.FirstOrDefault()?.Title ?? "this manual";

            Versions =
            [
                .. orderedVersions
                    .Skip((PageNumber - 1) * PageSize)
                    .Take(PageSize)
                    .Select(version => new DocumentHistoryRow(
                        version.Id,
                        version.Title,
                        $"{version.VersionMajor}.0",
                        FormatEffectiveDateRange(version.EffectiveDateFrom, version.EffectiveDateTo)))
            ];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.DocumentHistoryLoadFailed(exception, staticReportId);

            HasError = true;
        }
    }

    public string BuildPageUrl(int targetPage)
    {
        var safePage = Math.Clamp(targetPage, 1, TotalPages);
        return $"?staticReportId={StaticReportId}&sortOrder={SortOrder}&pageSize={PageSize}&pageNumber={safePage}";
    }

    public string BuildSortUrl()
    {
        var nextSortOrder = SortOrder == "desc" ? "asc" : "desc";
        return $"?staticReportId={StaticReportId}&sortOrder={nextSortOrder}&pageSize={PageSize}&pageNumber=1";
    }

    private static IReadOnlyList<StaticReportVersionDto> SortVersions(IReadOnlyList<StaticReportVersionDto> history, string sortOrder) =>
        string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase)
            ? history.OrderBy(version => version.VersionMajor).ToList()
            : history.OrderByDescending(version => version.VersionMajor).ToList();

    private static int ResolvePageSize(int requestedPageSize) =>
        requestedPageSize is > 0 && PageSizeOptions.Contains(requestedPageSize)
            ? requestedPageSize
            : DefaultPageSize;

    private static string FormatEffectiveDateRange(DateTime effectiveDateFrom, DateTime? effectiveDateTo)
    {
        var from = effectiveDateFrom.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

        return effectiveDateTo is null
            ? $"{from} -"
            : $"{from} - {effectiveDateTo.Value:dd/MM/yyyy}";
    }
}

public sealed record DocumentHistoryRow(Guid Id, string Title, string Version, string EffectiveDateRange);

/// <summary>Source-generated structured log messages for <see cref="DocumentHistoryModel"/>.</summary>
internal static partial class DocumentHistoryLog
{
    [LoggerMessage(EventId = 3010, Level = LogLevel.Error, Message = "Failed to load history for static report {StaticReportId} from CDC.Api")]
    public static partial void DocumentHistoryLoadFailed(this ILogger logger, Exception exception, Guid staticReportId);
}
