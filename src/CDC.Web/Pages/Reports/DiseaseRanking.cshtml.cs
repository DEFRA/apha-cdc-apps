using CDC.Common.Contracts;
using CDC.Web.Authorization.Constants;
using CDC.Web.Authorization.Services;
using CDC.Web.Features.DiseaseRanking;
using CDC.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.Reports;

/// <summary>
/// Lets a profile editor pick a disease ranking report and download its scored or ranked RFI graphs as a PDF,
/// replacing the legacy <c>DiseaseRanking.aspx</c>, <c>ShowDiseaseRankingReport.aspx</c> and
/// <c>ShowDiseaseRankingRFIReport.aspx</c> pages. Anyone else who opens it is sent to the stored
/// "DiseaseRankingReport" static report, as the legacy header and home page link did.
/// </summary>
[Authorize(Policy = AuthorizationPolicies.CanViewReports)]
public class DiseaseRankingModel(
    IApiClient apiClient,
    IDiseaseRankingApiService apiService,
    IDiseaseRankingPdfService pdfService,
    INavigationAuthorizationService navigationAuthorization,
    ILogger<DiseaseRankingModel> logger) : BreadcrumbPageModelBase("Disease ranking report")
{
    /// <summary>The title the stored report is uploaded under - see legacy <c>NavigationLinks.ascx.vb</c>.</summary>
    private const string StoredReportTitle = "DiseaseRankingReport";

    private static readonly Action<ILogger, Exception?> LogFailedToLoadOptionsMessage =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1, nameof(LogFailedToLoadOptionsMessage)),
            "Failed to load the disease ranking report options from CDC.Api.");

    private static readonly Action<ILogger, string, DiseaseRankingReportKind, Exception?> LogFailedToGenerateReportMessage =
        LoggerMessage.Define<string, DiseaseRankingReportKind>(
            LogLevel.Error,
            new EventId(2, nameof(LogFailedToGenerateReportMessage)),
            "Failed to generate disease ranking report {ReportKey} ({Kind}).");

    private static readonly Action<ILogger, Exception?> LogFailedToLoadStoredReportMessage =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(3, nameof(LogFailedToLoadStoredReportMessage)),
            "Failed to load the stored disease ranking report from CDC.Api.");

    /// <summary>The report chosen for the scored RFI graphs.</summary>
    [BindProperty]
    public string? ScoredReportKey { get; set; }

    /// <summary>The report chosen for the ranked RFI graphs.</summary>
    [BindProperty]
    public string? RankedReportKey { get; set; }

    /// <summary>Gets the reports available to select.</summary>
    public IReadOnlyList<DiseaseRankingReportOptionDto> Options { get; private set; } = [];

    /// <summary>Gets a value indicating whether the user may generate the reports; anyone else is only offered the stored report.</summary>
    public bool CanGenerateReports { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        CanGenerateReports = await CanGenerateReportsAsync();

        if (!CanGenerateReports)
        {
            return await ShowStoredReportAsync(cancellationToken);
        }

        await LoadOptionsAsync(cancellationToken);

        return Page();
    }

    public Task<IActionResult> OnPostScoredAsync(CancellationToken cancellationToken) =>
        DownloadAsync(ScoredReportKey, DiseaseRankingReportKind.Scored, nameof(ScoredReportKey), cancellationToken);

    public Task<IActionResult> OnPostRankedAsync(CancellationToken cancellationToken) =>
        DownloadAsync(RankedReportKey, DiseaseRankingReportKind.Ranked, nameof(RankedReportKey), cancellationToken);

    private async Task<IActionResult> DownloadAsync(
        string? reportKey,
        DiseaseRankingReportKind kind,
        string fieldName,
        CancellationToken cancellationToken)
    {
        CanGenerateReports = await CanGenerateReportsAsync();

        if (!CanGenerateReports)
        {
            return Forbid();
        }

        var normalizedReportKey = reportKey?.Trim();

        if (string.IsNullOrWhiteSpace(normalizedReportKey))
        {
            ModelState.AddModelError(fieldName, "Select a report");
            await LoadOptionsAsync(cancellationToken);

            return Page();
        }

        try
        {
            var pdf = await pdfService.GenerateAsync(normalizedReportKey, kind, cancellationToken);

            if (pdf is not null)
            {
                return File(pdf.Content, "application/pdf", pdf.FileName);
            }

            ModelState.AddModelError(fieldName, "Select a report from the list");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailedToGenerateReportMessage(logger, normalizedReportKey, kind, ex);
            ModelState.AddModelError(fieldName, "The report could not be generated. Try again later");
        }

        await LoadOptionsAsync(cancellationToken);

        return Page();
    }

    private Task<bool> CanGenerateReportsAsync() =>
        navigationAuthorization.CanAccessAsync(User, AuthorizationPolicies.CanViewDiseaseRankingReport);

    private async Task<IActionResult> ShowStoredReportAsync(CancellationToken cancellationToken)
    {
        try
        {
            var reports = await apiClient.GetCurrentStaticReportsAsync(isUserManual: false, publicOnly: false, cancellationToken);
            var storedReport = reports.FirstOrDefault(report => report.Title == StoredReportTitle);

            return storedReport is null
                ? NotFound()
                : Redirect($"/static-reports/versions/{storedReport.Id}/document?download=true");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            LogFailedToLoadStoredReportMessage(logger, ex);
            ModelState.AddModelError(string.Empty, "The report could not be loaded. Try again later");

            return Page();
        }
    }

    private async Task LoadOptionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            Options = await apiService.GetReportOptionsAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailedToLoadOptionsMessage(logger, ex);
            Options = [];
            ModelState.AddModelError(string.Empty, "The list of reports could not be loaded. Try again later");
        }
    }
}
