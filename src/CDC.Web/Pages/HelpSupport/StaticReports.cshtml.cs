using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.HelpSupport;

public class StaticReportsModel : BreadcrumbPageModelBase
{
    private readonly IApiClient apiClient;
    private readonly ILogger<StaticReportsModel> logger;
    private readonly string apiBaseUrl;

    /// <summary>Set to 1 for the user manuals list ("Help Using D2R2"), matching the legacy
    /// <c>StaticReports.aspx?UserManual=1</c> querystring.</summary>
    [BindProperty(SupportsGet = true)]
    public int UserManual { get; set; }

    public bool IsUserManual => UserManual == 1;

    /// <summary>Gets or sets the number of rows shown per page, or "All" for no paging.</summary>
    [BindProperty(SupportsGet = true)]
    public string PageSize { get; set; } = PageSizeOptions[0];

    /// <summary>Gets or sets the current 1-based results page.</summary>
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public static IReadOnlyList<string> PageSizeOptions { get; } = ["10", "15", "20", "30", "All"];

    /// <summary>Gets every current report or manual matching <see cref="IsUserManual"/>, before paging.</summary>
    public IReadOnlyList<StaticReportListItemDto> Reports { get; private set; } = [];

    /// <summary>Gets the single page of <see cref="Reports"/> to render.</summary>
    public IReadOnlyList<StaticReportListItemDto> PagedReports { get; private set; } = [];

    public int TotalResultCount { get; private set; }

    public int TotalPages { get; private set; } = 1;

    public StaticReportsModel(IApiClient apiClient, IConfiguration configuration, ILogger<StaticReportsModel> logger)
        : base("Static reports")
    {
        this.apiClient = apiClient;
        this.logger = logger;
        apiBaseUrl = (configuration["Api:BaseUrl"] ?? "http://localhost").TrimEnd('/');
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        Breadcrumb = new BreadcrumbViewModel(IsUserManual ? "Help Using D2R2" : "Static reports");

        try
        {
            Reports = await apiClient.GetCurrentStaticReportsAsync(IsUserManual, publicOnly: false, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load static reports from CDC.Api (UserManual={IsUserManual}).", IsUserManual);
            Reports = [];
        }

        TotalResultCount = Reports.Count;

        var pageSize = ResolvePageSize(PageSize);
        TotalPages = pageSize > 0 ? Math.Max(1, (int)Math.Ceiling(TotalResultCount / (double)pageSize)) : 1;
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);

        PagedReports = pageSize > 0
            ? [.. Reports.Skip((PageNumber - 1) * pageSize).Take(pageSize)]
            : Reports;

        return Page();
    }

    public string GetDocumentUrl(Guid staticReportVersionId) =>
        $"{apiBaseUrl}/api/static-reports/versions/{staticReportVersionId}/document";

    /// <summary>Builds the querystring URL for a results page link, preserving <see cref="UserManual"/> and <see cref="PageSize"/>.</summary>
    public string? BuildPageUrl(int page) => Url.Page("/HelpSupport/StaticReports", new { UserManual, PageSize, PageNumber = page });

    /// <summary>Formats the effective date range the way the legacy grid's "Effective dates"
    /// column did: "from -" while current, or "from - to" once superseded.</summary>
    public static string FormatEffectiveDates(StaticReportListItemDto report) =>
        report.EffectiveDateTo is null
            ? $"{report.EffectiveDateFrom:dd/MM/yyyy} -"
            : $"{report.EffectiveDateFrom:dd/MM/yyyy} - {report.EffectiveDateTo:dd/MM/yyyy}";

    /// <summary>Resolves the "Items per page" selection to a page size, where 0 means "All" (no paging).</summary>
    private static int ResolvePageSize(string pageSize)
    {
        if (string.Equals(pageSize, "All", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return int.TryParse(pageSize, out var parsedPageSize) ? parsedPageSize : 10;
    }
}
