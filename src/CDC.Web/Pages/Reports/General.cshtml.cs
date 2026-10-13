using CDC.Web.Authorization.Constants;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CDC.Web.Pages.Reports;

public class GeneralModel : BreadcrumbPageModelBase
{
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

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public string? UploadError { get; private set; }

    public bool CanManageReports { get; private set; }

    public bool IsHistoryPage => StaticReportId is Guid staticReportId && staticReportId != Guid.Empty;

    public string ReportTitle { get; private set; } = "General reports";

    public IReadOnlyList<StaticReportListItemDto> Reports { get; private set; } = [];

    public GeneralModel(
        IApiClient apiClient,
        IAuthorizationService authorizationService,
        IOptions<ApiOptions> apiOptions,
        ILogger<GeneralModel> logger)
        : base("General reports")
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
            return RedirectToPage("/Reports/General");
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
            logger.LogError(ex, "Failed to delete static report version {StaticReportVersionId}.", reportVersionId);
            ErrorMessage = "The report version could not be deleted.";
        }

        return RedirectToPage("/Reports/General");
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
                    return RedirectToPage("/Reports/General");
                }

                error = "Failed to upload the report. Try again.";
            }
            catch (StaticReportUploadRejectedException ex)
            {
                error = ex.Message;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to upload a static report.");
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
                ReportTitle = Reports.FirstOrDefault()?.Title ?? "General reports";
            }
            else
            {
                Reports = await apiClient.GetCurrentStaticReportsAsync(isUserManual: false, publicOnly: false, cancellationToken);
                ReportTitle = "General reports";
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load general reports.");
            Reports = [];
        }
    }

    public async Task<IActionResult> OnPostMakePublicAsync(Guid reportVersionId, CancellationToken cancellationToken)
    {
        if (reportVersionId == Guid.Empty)
        {
            return RedirectToPage("/Reports/General", new { StaticReportId = StaticReportId });
        }

        try
        {
            var isSuccess = await apiClient.SetStaticReportPublicAccessAsync(reportVersionId, cancellationToken);
            if (!isSuccess)
            {
                logger.LogWarning("The public-access change request for static report version {StaticReportVersionId} was rejected.", reportVersionId);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to make static report version {StaticReportVersionId} public.", reportVersionId);
        }

        return RedirectToPage("/Reports/General", new { StaticReportId = StaticReportId });
    }

    public string GetDocumentUrl(Guid staticReportVersionId) =>
        $"{apiBaseUrl}/api/static-reports/versions/{staticReportVersionId}/document";

    public string? BuildHistoryUrl(Guid staticReportId) =>
        Url.Page("/Reports/General", new { StaticReportId = staticReportId });

    public static string FormatEffectiveDates(StaticReportListItemDto report) =>
        report.EffectiveDateTo is null
            ? $"{report.EffectiveDateFrom:dd/MM/yyyy} -"
            : $"{report.EffectiveDateFrom:dd/MM/yyyy} - {report.EffectiveDateTo:dd/MM/yyyy}";
}
