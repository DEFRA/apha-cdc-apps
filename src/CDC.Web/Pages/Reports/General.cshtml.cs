using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CDC.Web.Pages.Reports;

public class GeneralModel : BreadcrumbPageModelBase
{
    private readonly IApiClient apiClient;
    private readonly string apiBaseUrl;
    private readonly ILogger<GeneralModel> logger;

    [BindProperty(SupportsGet = true)]
    public Guid? StaticReportId { get; set; }

    public bool IsHistoryPage => StaticReportId is Guid staticReportId && staticReportId != Guid.Empty;

    public string ReportTitle { get; private set; } = "General reports";

    public IReadOnlyList<StaticReportListItemDto> Reports { get; private set; } = [];

    public GeneralModel(IApiClient apiClient, IOptions<ApiOptions> apiOptions, ILogger<GeneralModel> logger)
        : base("General reports")
    {
        this.apiClient = apiClient;
        apiBaseUrl = (apiOptions.Value.BaseUrl ?? string.Empty).TrimEnd('/');
        this.logger = logger;
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
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

        return Page();
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
