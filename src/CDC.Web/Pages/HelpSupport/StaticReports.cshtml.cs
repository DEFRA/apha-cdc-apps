using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CDC.Web.Pages.HelpSupport;

/// <summary>
/// Replaces the legacy <c>StaticReports.aspx</c> page: the static reports / "Help Using D2R2"
/// user manuals list (<see cref="StaticReportId"/> absent), or one report's version history
/// (<see cref="StaticReportId"/> present) - matching the legacy page's dual-mode behaviour.
/// </summary>
public class StaticReportsModel : BreadcrumbPageModelBase
{
    private static readonly Action<ILogger, bool, Exception?> LogFailedToLoadStaticReportsMessage =
        LoggerMessage.Define<bool>(
            LogLevel.Error,
            new EventId(1, nameof(LogFailedToLoadStaticReportsMessage)),
            "Failed to load static reports from CDC.Api (UserManual={IsUserManual}).");
    private static readonly Action<ILogger, Guid, Exception?> LogFailedToLoadStaticReportHistoryMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Error,
            new EventId(2, nameof(LogFailedToLoadStaticReportHistoryMessage)),
            "Failed to load static report history from CDC.Api (StaticReportId={StaticReportId}).");
    private static readonly Action<ILogger, Guid, Exception?> LogDeletedStaticReportVersionMessage =
        LoggerMessage.Define<Guid>(
            LogLevel.Information,
            new EventId(3, nameof(LogDeletedStaticReportVersionMessage)),
            "Deleted static report version {StaticReportVersionId}");
    private static readonly Action<ILogger, Guid, string, Exception?> LogFailedToDeleteStaticReportVersionMessage =
        LoggerMessage.Define<Guid, string>(
            LogLevel.Error,
            new EventId(4, nameof(LogFailedToDeleteStaticReportVersionMessage)),
            "Failed to delete static report version {StaticReportVersionId}: {ErrorMessage}");
    private static readonly Action<ILogger, bool, string, Exception?> LogUploadedStaticReportMessage =
        LoggerMessage.Define<bool, string>(
            LogLevel.Information,
            new EventId(5, nameof(LogUploadedStaticReportMessage)),
            "Uploaded a static report (UserManual={IsUserManual}, Title={Title})");
    private static readonly Action<ILogger, string, Exception?> LogFailedToUploadStaticReportMessage =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(6, nameof(LogFailedToUploadStaticReportMessage)),
            "Failed to upload a static report: {ErrorMessage}");

    private readonly IApiClient apiClient;
    private readonly ILogger<StaticReportsModel> logger;
    private readonly string apiBaseUrl;

    /// <summary>Set to 1 for the user manuals list ("Help Using D2R2"), matching the legacy
    /// <c>StaticReports.aspx?UserManual=1</c> querystring.</summary>
    [BindProperty(SupportsGet = true)]
    public int UserManual { get; set; }

    public bool IsUserManual => UserManual == 1;

    /// <summary>Set to view one report's version history, matching the legacy
    /// <c>StaticReports.aspx?StaticReportId=...</c> querystring. Hides History, Delete and Add.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? StaticReportId { get; set; }

    public bool IsHistoryMode => StaticReportId.HasValue;

    /// <summary>Gets or sets the number of rows shown per page, or "All" for no paging.</summary>
    [BindProperty(SupportsGet = true)]
    public string PageSize { get; set; } = PageSizeOptions[0];

    /// <summary>Gets or sets the current 1-based results page.</summary>
    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    /// <summary>Gets or sets whether the "Upload a user manual"/"Upload a static report" panel is
    /// open, matching the legacy <c>pnlReportUpload.Visible</c>.</summary>
    [BindProperty(SupportsGet = true)]
    public bool ShowUploadPanel { get; set; }

    /// <summary>Gets or sets the file chosen in the upload panel.</summary>
    [BindProperty]
    public IFormFile? UploadedFile { get; set; }

    /// <summary>Gets or sets whether the uploaded version should be made public. Only offered for
    /// general reports - matches legacy's <c>cbPublic.Visible = False</c> for user manuals.</summary>
    [BindProperty]
    public bool MakePublic { get; set; }

    public static IReadOnlyList<string> PageSizeOptions { get; } = ["10", "15", "20", "30", "All"];

    /// <summary>Gets every current report/manual, or every version of one report, before paging.</summary>
    public IReadOnlyList<StaticReportListItemDto> Reports { get; private set; } = [];

    /// <summary>Gets the single page of <see cref="Reports"/> to render.</summary>
    public IReadOnlyList<StaticReportListItemDto> PagedReports { get; private set; } = [];

    public int TotalResultCount { get; private set; }

    public int TotalPages { get; private set; } = 1;

    /// <summary>Gets a value indicating whether the current user may upload documents, matching
    /// the legacy <c>btnAdd.Visible</c>. Always <see langword="false"/> in history mode.</summary>
    public bool CanUpload { get; private set; }

    /// <summary>Gets the success message to show after an upload or delete, carried across the redirect.</summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>Gets the error message to show after a failed upload or delete, carried across the redirect.</summary>
    [TempData]
    public string? ErrorMessage { get; set; }

    public StaticReportsModel(IApiClient apiClient, IOptions<ApiOptions> apiOptions, ILogger<StaticReportsModel> logger)
        : base("Static reports")
    {
        this.apiClient = apiClient;
        this.logger = logger;
        apiBaseUrl = (apiOptions.Value.BaseUrl ?? string.Empty).TrimEnd('/');
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);

        return Page();
    }

    /// <summary>
    /// Uploads the chosen file as a new version, matching the legacy <c>btnSave_Click</c> handler:
    /// rejects non-PDF files and filenames under 5 characters before calling the API.
    /// </summary>
    public async Task<IActionResult> OnPostUploadAsync(CancellationToken cancellationToken)
    {
        var fileType = IsUserManual ? "user manual" : "report";

        if (UploadedFile is null || UploadedFile.Length == 0)
        {
            ErrorMessage = $"Failed to upload the {fileType}: Please choose a file to upload";
        }
        else if (UploadedFile.ContentType != "application/pdf")
        {
            ErrorMessage = $"Failed to upload the {fileType}: The uploaded {fileType} must be a Pdf";
        }
        else if (UploadedFile.FileName.Length < 5)
        {
            ErrorMessage = $"Failed to upload the {fileType}: The filename was less than 5 characters long";
        }
        else
        {
            var title = UploadedFile.FileName[..^4];

            await using var stream = new MemoryStream();
            await UploadedFile.CopyToAsync(stream, cancellationToken);

            var result = await apiClient.UploadStaticReportAsync(
                title, stream.ToArray(), IsUserManual, isPublic: !IsUserManual && MakePublic, cancellationToken);

            if (result.Outcome == UploadStaticReportOutcome.Success)
            {
                LogUploadedStaticReportMessage(logger, IsUserManual, title, null);
                SuccessMessage = $"The {fileType} was uploaded successfully";
            }
            else
            {
                LogFailedToUploadStaticReportMessage(logger, result.ErrorMessage ?? string.Empty, null);
                ErrorMessage = $"Failed to upload the {fileType}: {result.ErrorMessage}";
            }
        }

        return RedirectToPage(new { UserManual, PageNumber, PageSize });
    }

    /// <summary>
    /// Deletes one version and refreshes the list, matching the legacy <c>grdReports_RowCommand</c>
    /// "Delete" case.
    /// </summary>
    public async Task<IActionResult> OnPostDeleteAsync(Guid staticReportVersionId, CancellationToken cancellationToken)
    {
        var fileType = IsUserManual ? "user manual" : "report";
        var result = await apiClient.DeleteStaticReportVersionAsync(staticReportVersionId, cancellationToken);

        if (result.Outcome == DeleteStaticReportVersionOutcome.Success)
        {
            LogDeletedStaticReportVersionMessage(logger, staticReportVersionId, null);
            SuccessMessage = $"The {fileType} version was successfully deleted";
        }
        else
        {
            LogFailedToDeleteStaticReportVersionMessage(logger, staticReportVersionId, result.ErrorMessage ?? string.Empty, null);
            ErrorMessage = $"The {fileType} version could not be deleted: {result.ErrorMessage}";
        }

        return RedirectToPage(new { UserManual, PageNumber, PageSize });
    }

    public string GetDocumentUrl(Guid staticReportVersionId) =>
        $"{apiBaseUrl}/api/static-reports/versions/{staticReportVersionId}/document";

    /// <summary>Builds the querystring URL for a results page link, preserving the current mode and page size.</summary>
    public string? BuildPageUrl(int page) =>
        Url.Page("/HelpSupport/StaticReports", new { UserManual, StaticReportId, PageSize, PageNumber = page });

    /// <summary>Builds the "History" link URL for a report row, matching legacy's
    /// <c>StaticReports.aspx?StaticReportId=...&amp;UserManual=...</c>.</summary>
    public string? BuildHistoryUrl(Guid staticReportId) =>
        Url.Page("/HelpSupport/StaticReports", new { StaticReportId = staticReportId, UserManual });

    /// <summary>Formats the effective date range the way the legacy grid's "Effective dates"
    /// column did: "from -" while current, or "from - to" once superseded.</summary>
    public static string FormatEffectiveDates(StaticReportListItemDto report) =>
        report.EffectiveDateTo is null
            ? $"{report.EffectiveDateFrom:dd/MM/yyyy} -"
            : $"{report.EffectiveDateFrom:dd/MM/yyyy} - {report.EffectiveDateTo:dd/MM/yyyy}";

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (IsHistoryMode)
        {
            await LoadHistoryAsync(cancellationToken);
        }
        else
        {
            await LoadCurrentReportsAsync(cancellationToken);
        }

        TotalResultCount = Reports.Count;

        var pageSize = ResolvePageSize(PageSize);
        TotalPages = pageSize > 0 ? Math.Max(1, (int)Math.Ceiling(TotalResultCount / (double)pageSize)) : 1;
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);

        PagedReports = pageSize > 0
            ? [.. Reports.Skip((PageNumber - 1) * pageSize).Take(pageSize)]
            : Reports;
    }

    private async Task LoadCurrentReportsAsync(CancellationToken cancellationToken)
    {
        var listPageName = IsUserManual ? "Help Using D2R2" : "Static reports";
        Breadcrumb = new BreadcrumbViewModel(listPageName);

        try
        {
            Reports = await apiClient.GetCurrentStaticReportsAsync(IsUserManual, publicOnly: false, cancellationToken);
            CanUpload = await apiClient.CanUploadStaticReportsAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            LogFailedToLoadStaticReportsMessage(logger, IsUserManual, ex);
            Reports = [];
            CanUpload = false;
        }
    }

    private async Task LoadHistoryAsync(CancellationToken cancellationToken)
    {
        try
        {
            Reports = await apiClient.GetStaticReportHistoryAsync(StaticReportId!.Value, publicOnly: false, cancellationToken);
        }
        catch (Exception ex)
        {
            LogFailedToLoadStaticReportHistoryMessage(logger, StaticReportId!.Value, ex);
            Reports = [];
        }

        // Legacy infers IsUserManual/the title for the history page from the first returned row,
        // falling back to the UserManual querystring flag when the report has since been deleted.
        var firstVersion = Reports.Count > 0 ? Reports[0] : null;
        var isUserManual = firstVersion?.IsUserManual ?? IsUserManual;
        var pageName = firstVersion is not null ? $"History for {firstVersion.Title}" : "History";
        var parentPageName = isUserManual ? "Help Using D2R2" : "Static reports";
        var parentUrl = Url.Page("/HelpSupport/StaticReports", new { UserManual = isUserManual ? 1 : 0 });

        Breadcrumb = new BreadcrumbViewModel(pageName, parentPageName, parentUrl);
    }

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
