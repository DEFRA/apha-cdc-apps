using CDC.Web.Authorization.Constants;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Downloads a profile's contributions report as a PDF. Replaces the legacy
/// <c>ShowContributionReport.aspx</c>, which streamed a TallPDF document; the report is now a Razor
/// view printed to PDF by Playwright's headless Chromium. Reached from the search results card and
/// the Manage profile page; the page itself only shows when the report cannot be produced.
/// </summary>
/// <param name="reportsApiService">Typed client for the profile report endpoints on CDC.Api.</param>
/// <param name="viewRenderer">Renders the report's Razor view to HTML.</param>
/// <param name="pdfService">Prints the HTML to PDF.</param>
/// <param name="logger">Structured logger.</param>
[Authorize(Policy = AuthorizationPolicies.CanViewContributionsReport)]
public class ViewContributionsReportModel(
    IProfileReportsApiService reportsApiService,
    IRazorViewRenderer viewRenderer,
    IPlaywrightPdfService pdfService,
    ILogger<ViewContributionsReportModel> logger) : PageModel
{
    /// <summary>The Razor view that lays the report out.</summary>
    public const string ReportViewPath = "/Views/Pdf/ContributionsReport.cshtml";

    /// <summary>The message shown when the report could not be produced.</summary>
    public const string FailureMessage = "We could not create the contributions report. Try again later.";

    /// <summary>Gets or sets the profile to report on, bound from the page route.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid ProfileId { get; set; }

    /// <summary>Gets the message shown when the report could not be produced, otherwise <see langword="null"/>.</summary>
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var result = await LoadReportAsync(cancellationToken);

        switch (result.Outcome)
        {
            case ContributionsReportOutcome.NotFound:
                return NotFound();
            case ContributionsReportOutcome.Forbidden:
                logger.ContributionsReportRefused(ProfileId, result.Outcome);
                return StatusCode(StatusCodes.Status403Forbidden);
            case ContributionsReportOutcome.Error:
                ErrorMessage = FailureMessage;
                return Page();
        }

        var layout = ContributionsReportLayout.Build(result.Report!);

        try
        {
            var html = await viewRenderer.RenderAsync(ReportViewPath, layout);
            var pdf = await pdfService.RenderAsync(html, ContributionsReportLayout.BuildPdfOptions(layout.ProfileTitle), cancellationToken);

            return File(pdf, "application/pdf", ContributionsReportLayout.BuildFileName(layout.ProfileTitle));
        }
        catch (Exception exception) when (exception is PdfRenderException or InvalidOperationException)
        {
            logger.ContributionsReportRenderFailed(exception, ProfileId);
            ErrorMessage = FailureMessage;

            return Page();
        }
    }

    private async Task<ContributionsReportResult> LoadReportAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await reportsApiService.GetContributionsReportAsync(ProfileId, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.ContributionsReportDataLoadFailed(exception, ProfileId);

            return new ContributionsReportResult(ContributionsReportOutcome.Error);
        }
    }
}
