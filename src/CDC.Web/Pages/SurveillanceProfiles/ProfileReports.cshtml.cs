using CDC.Common.Contracts;
using CDC.Web.Authorization.Services;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Infrastructure.Reports;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Lists a profile version's reports with links to download each PDF and, where permitted, to
/// regenerate it. Replaces the legacy <c>ProfileReports.aspx</c> (and <c>ShowPublishedProfile.aspx</c>,
/// which streamed a stored PDF); regeneration builds the PDF with a Razor view and Playwright and
/// stores it, as the legacy <c>CreateProfileReport</c> did with TallPDF.
/// </summary>
/// <param name="reportsApiService">Typed client for the profile report endpoints on CDC.Api.</param>
/// <param name="reportGenerator">Generates report PDFs by report name.</param>
/// <param name="navigationAuthorization">Evaluates the permissions that decide what the page shows.</param>
/// <param name="logger">Structured logger.</param>
public class ProfileReportsModel(
    IProfileReportsApiService reportsApiService,
    IProfileReportGenerator reportGenerator,
    INavigationAuthorizationService navigationAuthorization,
    ILogger<ProfileReportsModel> logger) : PageModel
{
    /// <summary>The message shown when the reports could not be loaded.</summary>
    public const string LoadFailureMessage = "We could not load the profile reports. Try again later.";

    /// <summary>The message shown when a stored report could not be downloaded.</summary>
    public const string DownloadFailureMessage = "We could not download the report. Try again later.";

    /// <summary>Gets or sets the profile version whose reports are listed, bound from the page route.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid ProfileVersionId { get; set; }

    /// <summary>Gets or sets the message shown after a report was regenerated, carried across the redirect.</summary>
    [TempData]
    public string? SuccessMessage { get; set; }

    /// <summary>Gets or sets the message shown after regenerating a report failed, carried across the redirect.</summary>
    [TempData]
    public string? RegenerationErrorMessage { get; set; }

    /// <summary>Gets the overview shown on the page, otherwise <see langword="null"/>.</summary>
    public ProfileReportsOverviewDto? Overview { get; private set; }

    /// <summary>Gets the message shown when the page could not be loaded, otherwise <see langword="null"/>.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Gets a value indicating whether the intro text is shown, which the legacy page hid from profile editors.</summary>
    public bool ShowIntroText { get; private set; }

    /// <summary>Gets a value indicating whether the "Create bespoke report" button is shown.</summary>
    public bool CanCreateBespokeReport { get; private set; }

    /// <summary>Gets the report table's rows.</summary>
    public IReadOnlyList<ProfileReportRow> Rows { get; private set; } = [];

    /// <summary>Gets the page title, "Profile reports for {title} v{version}", as the legacy page set it.</summary>
    public string PageTitle => Overview is null
        ? "Profile reports"
        : $"Profile reports for {ProfileTitleHtmlFormatter.ToPlainText(Overview.FullTitle)} v{Overview.VersionLabel}";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken) =>
        await LoadAsync(cancellationToken) ?? Page();

    /// <summary>Downloads a stored report PDF, named "{profile title}_{report name}" as the legacy page did.</summary>
    /// <param name="reportId">The report to download.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The PDF, or the page when it cannot be loaded.</returns>
    public async Task<IActionResult> OnGetDownloadAsync(Guid reportId, CancellationToken cancellationToken)
    {
        var failure = await LoadAsync(cancellationToken);

        if (failure is not null)
        {
            return failure;
        }

        var report = Overview!.Reports.FirstOrDefault(candidate => candidate.Id == reportId && candidate.HasPdfData);

        if (report is null)
        {
            return NotFound();
        }

        byte[]? pdf;

        try
        {
            pdf = await reportsApiService.GetStoredReportAsync(ProfileVersionId, reportId, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.ProfileReportDownloadFailed(exception, reportId, ProfileVersionId);
            ErrorMessage = DownloadFailureMessage;

            return Page();
        }

        return pdf is null
            ? NotFound()
            : File(pdf, "application/pdf", PdfFileName.Build($"{ProfileTitleHtmlFormatter.ToPlainText(Overview.FullTitle)}_{report.DisplayName}"));
    }

    /// <summary>Regenerates one report's PDF and stores it, then redirects back to the list with the outcome.</summary>
    /// <param name="reportId">The report to regenerate.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>A redirect to the list, or an error status when the report may not be regenerated.</returns>
    public async Task<IActionResult> OnPostRegenerateAsync(Guid reportId, CancellationToken cancellationToken)
    {
        var failure = await LoadAsync(cancellationToken);

        if (failure is not null)
        {
            return failure;
        }

        var report = Overview!.Reports.FirstOrDefault(candidate => candidate.Id == reportId);

        if (report is null || !reportGenerator.CanGenerate(report.ReportName))
        {
            return NotFound();
        }

        if (!Overview.CanCreateReports)
        {
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        await RegenerateAsync(report, cancellationToken);

        return RedirectToPage(new { profileVersionId = ProfileVersionId });
    }

    private async Task RegenerateAsync(ProfileReportListItemDto report, CancellationToken cancellationToken)
    {
        try
        {
            var pdf = await reportGenerator.GenerateAsync(report.ReportName, ProfileVersionId, cancellationToken);
            var outcome = await reportsApiService.SaveReportAsync(ProfileVersionId, report.Id, report.ReportName, pdf, cancellationToken);

            if (outcome == ProfileReportsOutcome.Success)
            {
                SuccessMessage = $"{report.DisplayName}: Report generation succeeded";

                return;
            }

            logger.ProfileReportStoreRefused(report.ReportName, ProfileVersionId, outcome);
        }
        catch (Exception exception) when (exception is PdfRenderException or HttpRequestException or TaskCanceledException or InvalidOperationException or NotSupportedException)
        {
            logger.ProfileReportRegenerationFailed(exception, report.ReportName, ProfileVersionId);
        }

        RegenerationErrorMessage = $"{report.DisplayName}: Report generation failed";
    }

    /// <summary>Loads the overview and what the user may do with it; returns a result only when the page cannot be shown.</summary>
    private async Task<IActionResult?> LoadAsync(CancellationToken cancellationToken)
    {
        ProfileReportsOverviewResult result;

        try
        {
            result = await reportsApiService.GetOverviewAsync(ProfileVersionId, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.ProfileReportsLoadFailed(exception, ProfileVersionId);
            ErrorMessage = LoadFailureMessage;

            return Page();
        }

        switch (result.Outcome)
        {
            case ProfileReportsOutcome.NotFound:
                return NotFound();
            case ProfileReportsOutcome.Forbidden:
                return StatusCode(StatusCodes.Status403Forbidden);
            case ProfileReportsOutcome.Error:
                ErrorMessage = LoadFailureMessage;

                return Page();
        }

        Overview = result.Overview;
        ShowIntroText = !await navigationAuthorization.IsProfileEditorAsync(User);
        CanCreateBespokeReport = await navigationAuthorization.CanViewBespokeReportAsync(User);
        Rows =
        [
            .. Overview!.Reports.Select(report => new ProfileReportRow(
                report.Id,
                report.DisplayName,
                report.HasPdfData,
                Overview.CanCreateReports && reportGenerator.CanGenerate(report.ReportName)))
        ];

        return null;
    }
}
