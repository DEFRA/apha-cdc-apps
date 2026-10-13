using CDC.Web.Authorization.Constants;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CDC.Web.Pages.Reports;

public partial class GeneralModel : BreadcrumbPageModelBase
{
    private const string ReportsPage = "/Reports/General";
    private const string ReportsTitle = "General reports";
    private const long MaxUploadBytes = 20 * 1024 * 1024;
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

    private readonly IApiClient apiClient;
    private readonly IAuthorizationService authorizationService;
    private readonly string apiBaseUrl;
    private readonly ILogger<GeneralModel> logger;

    [BindProperty(SupportsGet = true)]
    public Guid? StaticReportId { get; set; }

    [BindProperty(SupportsGet = true)]
    public bool ShowUpload { get; set; }

    [BindProperty]
    public IFormFile? ReportFile { get; set; }

    [BindProperty]
    public bool MakePublic { get; set; }

    [BindProperty(SupportsGet = true)]
    public string PageSize { get; set; } = PageSizeOptions[0];

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    public static IReadOnlyList<string> PageSizeOptions { get; } = ["10", "15", "20", "30", "All"];

    public IReadOnlyList<StaticReportListItemDto> PagedReports { get; private set; } = [];

    public int TotalPages { get; private set; } = 1;

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public string? UploadError { get; private set; }

    public bool CanManageReports { get; private set; }

    public bool IsHistoryPage => StaticReportId is Guid staticReportId && staticReportId != Guid.Empty;

    public string ReportTitle { get; private set; } = ReportsTitle;

    public IReadOnlyList<StaticReportListItemDto> Reports { get; private set; } = [];

    public GeneralModel(
        IApiClient apiClient,
        IAuthorizationService authorizationService,
        IOptions<ApiOptions> apiOptions,
        ILogger<GeneralModel> logger)
        : base(ReportsTitle)
    {
        this.apiClient = apiClient;
        this.authorizationService = authorizationService;
        apiBaseUrl = (apiOptions.Value.BaseUrl ?? string.Empty).TrimEnd('/');
        this.logger = logger;
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);

        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid reportVersionId, CancellationToken cancellationToken)
    {
        if (!await CheckCanManageAsync())
        {
            return Forbid();
        }

        if (reportVersionId == Guid.Empty)
        {
            return RedirectToPage(ReportsPage, new { PageSize, PageNumber });
        }

        try
        {
            if (await apiClient.DeleteStaticReportVersionAsync(reportVersionId, cancellationToken))
            {
                StatusMessage = "The report version was successfully deleted.";
            }
            else
            {
                ErrorMessage = "The report version could not be deleted.";
            }
        }
        catch (Exception ex)
        {
            LogDeleteFailed(ex, reportVersionId);
            ErrorMessage = "The report version could not be deleted.";
        }

        return RedirectToPage(ReportsPage, new { PageSize, PageNumber });
    }

    public async Task<IActionResult> OnPostUploadAsync(CancellationToken cancellationToken)
    {
        if (!await CheckCanManageAsync())
        {
            return Forbid();
        }

        ShowUpload = true;
        StaticReportId = null;

        var (error, title, pdfData) = await ValidateUploadAsync(cancellationToken);
        if (error is null)
        {
            try
            {
                if (await apiClient.UploadStaticReportAsync(title!, pdfData!, MakePublic, cancellationToken))
                {
                    StatusMessage = "The report was uploaded successfully.";
                    return RedirectToPage(ReportsPage);
                }

                error = "Failed to upload the report. Try again.";
            }
            catch (StaticReportUploadRejectedException ex)
            {
                error = ex.Message;
            }
            catch (Exception ex)
            {
                LogUploadFailed(ex);
                error = "Failed to upload the report. Try again.";
            }
        }

        UploadError = error;
        await LoadAsync(cancellationToken);
        return Page();
    }

    private async Task<(string? Error, string? Title, byte[]? PdfData)> ValidateUploadAsync(CancellationToken cancellationToken)
    {
        if (ReportFile is null || ReportFile.Length == 0)
        {
            return ("Choose a file to upload", null, null);
        }

        var title = Path.GetFileNameWithoutExtension(ReportFile.FileName);
        if (!string.Equals(Path.GetExtension(ReportFile.FileName), ".pdf", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(title))
        {
            return ("Choose a PDF file to upload", null, null);
        }

        if (ReportFile.Length > MaxUploadBytes)
        {
            return ("The file must be smaller than 20MB", null, null);
        }

        using var stream = new MemoryStream();
        await ReportFile.CopyToAsync(stream, cancellationToken);
        var pdfData = stream.ToArray();

        return pdfData.AsSpan().StartsWith(PdfSignature)
            ? (null, title.Trim(), pdfData)
            : ("Choose a PDF file to upload", null, null);
    }

    private async Task<bool> CheckCanManageAsync() =>
        (await authorizationService.AuthorizeAsync(User, AuthorizationPolicies.CanManageReports)).Succeeded;

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        CanManageReports = await CheckCanManageAsync();
        ShowUpload &= CanManageReports && !IsHistoryPage;

        try
        {
            if (IsHistoryPage)
            {
                Reports = await apiClient.GetStaticReportHistoryAsync(StaticReportId!.Value, publicOnly: false, cancellationToken);
                ReportTitle = Reports.Count > 0 ? Reports[0].Title : ReportsTitle;
            }
            else
            {
                Reports = await apiClient.GetCurrentStaticReportsAsync(isUserManual: false, publicOnly: false, cancellationToken);
                ReportTitle = ReportsTitle;
            }
        }
        catch (Exception ex)
        {
            LogLoadFailed(ex);
            Reports = [];
        }

        var pageSize = ResolvePageSize(PageSize);
        TotalPages = pageSize > 0 ? Math.Max(1, (int)Math.Ceiling(Reports.Count / (double)pageSize)) : 1;
        PageNumber = Math.Clamp(PageNumber, 1, TotalPages);
        PagedReports = pageSize > 0
            ? [.. Reports.Skip((PageNumber - 1) * pageSize).Take(pageSize)]
            : Reports;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to delete static report version {StaticReportVersionId}.")]
    private partial void LogDeleteFailed(Exception ex, Guid staticReportVersionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to upload a static report.")]
    private partial void LogUploadFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to load general reports.")]
    private partial void LogLoadFailed(Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "The public-access change request for static report version {StaticReportVersionId} was rejected.")]
    private partial void LogMakePublicRejected(Guid staticReportVersionId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to make static report version {StaticReportVersionId} public.")]
    private partial void LogMakePublicFailed(Exception ex, Guid staticReportVersionId);
    private static int ResolvePageSize(string pageSize)
    {
        if (string.Equals(pageSize, "All", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return int.TryParse(pageSize, out var parsedPageSize) && parsedPageSize > 0 ? parsedPageSize : 10;
    }

    /// <summary>Legacy <c>StaticReport.CanMakePublic</c>: non-public general reports, for report managers only.</summary>
    public bool CanMakePublic(StaticReportListItemDto report) =>
        CanManageReports && !report.IsPublic && !report.IsUserManual;

    public async Task<IActionResult> OnPostMakePublicAsync(Guid reportVersionId, CancellationToken cancellationToken)
    {
        if (!await CheckCanManageAsync())
        {
            return Forbid();
        }

        var redirectValues = new { StaticReportId, PageSize, PageNumber };

        if (reportVersionId == Guid.Empty)
        {
            return RedirectToPage(ReportsPage, redirectValues);
        }

        try
        {
            if (await apiClient.SetStaticReportPublicAccessAsync(reportVersionId, cancellationToken))
            {
                StatusMessage = "The report version was made public.";
            }
            else
            {
                LogMakePublicRejected(reportVersionId);
                ErrorMessage = "The report version could not be made public.";
            }
        }
        catch (Exception ex)
        {
            LogMakePublicFailed(ex, reportVersionId);
            ErrorMessage = "The report version could not be made public.";
        }

        return RedirectToPage(ReportsPage, redirectValues);
    }

    public string? BuildPageUrl(int page) =>
        Url.Page(ReportsPage, new { StaticReportId, PageSize, PageNumber = page });

    public string GetDocumentUrl(Guid staticReportVersionId) =>
        $"{apiBaseUrl}/api/static-reports/versions/{staticReportVersionId}/document";

    public string? BuildHistoryUrl(Guid staticReportId) =>
        Url.Page(ReportsPage, new { StaticReportId = staticReportId });

    public static string FormatEffectiveDates(StaticReportListItemDto report) =>
        report.EffectiveDateTo is null
            ? $"{report.EffectiveDateFrom:dd/MM/yyyy} -"
            : $"{report.EffectiveDateFrom:dd/MM/yyyy} - {report.EffectiveDateTo:dd/MM/yyyy}";
}
