using CDC.Common.Contracts;
using CDC.Web.Features.DiseaseRanking;
using CDC.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.Reports;

/// <summary>
/// Lets a user pick a disease ranking report and download its scored or ranked RFI graphs as a PDF,
/// replacing the legacy <c>DiseaseRanking.aspx</c>, <c>ShowDiseaseRankingReport.aspx</c> and
/// <c>ShowDiseaseRankingRFIReport.aspx</c> pages.
/// </summary>
public class DiseaseRankingModel(
    IDiseaseRankingApiService apiService,
    IDiseaseRankingPdfService pdfService,
    ILogger<DiseaseRankingModel> logger) : BreadcrumbPageModelBase("Disease ranking report")
{
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

    /// <summary>The report chosen for the scored RFI graphs.</summary>
    [BindProperty]
    public string? ScoredReportKey { get; set; }

    /// <summary>The report chosen for the ranked RFI graphs.</summary>
    [BindProperty]
    public string? RankedReportKey { get; set; }

    /// <summary>Gets the reports available to select.</summary>
    public IReadOnlyList<DiseaseRankingReportOptionDto> Options { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken) => await LoadOptionsAsync(cancellationToken);

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
        if (string.IsNullOrWhiteSpace(reportKey))
        {
            ModelState.AddModelError(fieldName, "Select a report");
            await LoadOptionsAsync(cancellationToken);

            return Page();
        }

        try
        {
            var pdf = await pdfService.GenerateAsync(reportKey, kind, cancellationToken);

            if (pdf is not null)
            {
                return File(pdf.Content, "application/pdf", pdf.FileName);
            }

            ModelState.AddModelError(fieldName, "Select a report from the list");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogFailedToGenerateReportMessage(logger, reportKey, kind, ex);
            ModelState.AddModelError(fieldName, "The report could not be generated. Try again later");
        }

        await LoadOptionsAsync(cancellationToken);

        return Page();
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
