using CDC.Web.Authorization.Constants;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Infrastructure.Reports;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Downloads one of the four user guidance reports as a PDF. Replaces the legacy <c>ShowProfileGuidanceReport.aspx</c>,
/// which streamed a TallPDF document; each report is now a Razor view printed to PDF by Playwright's headless Chromium.
/// Reached from the report buttons on the Edit questions and guidance page; the page itself only shows when the
/// report cannot be produced.
/// </summary>
/// <param name="questionsApiService">Typed client for the profile question endpoints on CDC.Api.</param>
/// <param name="viewRenderer">Renders the report's Razor view to HTML.</param>
/// <param name="pdfService">Prints the HTML to PDF.</param>
/// <param name="logger">Structured logger.</param>
/// <param name="timeProvider">Supplies the time the report is produced; defaults to the system clock.</param>
[Authorize(Policy = AuthorizationPolicies.CanEditUserGuidance)]
public class ViewProfileGuidanceReportModel(
    IProfileQuestionsApiService questionsApiService,
    IRazorViewRenderer viewRenderer,
    IPlaywrightPdfService pdfService,
    ILogger<ViewProfileGuidanceReportModel> logger,
    TimeProvider? timeProvider = null) : PageModel
{
    /// <summary>The Razor view that lays out the full, summary profile and Q&amp;A sheet reports.</summary>
    public const string GuidanceViewPath = "/Views/Pdf/GuidanceReport.cshtml";

    /// <summary>The Razor view that lays out the summary prioritisation report guidance report.</summary>
    public const string SprViewPath = "/Views/Pdf/SprGuidanceReport.cshtml";

    /// <summary>The message shown when the report could not be produced.</summary>
    public const string FailureMessage = "We could not create the guidance report. Try again later.";

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>Gets or sets the report to produce, as named in the query string; the full profile report when empty.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Type { get; set; }

    /// <summary>Gets the message shown when the report could not be produced, otherwise <see langword="null"/>.</summary>
    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!TryReadType(out var type))
        {
            logger.GuidanceReportTypeRejected(Type ?? string.Empty);

            return BadRequest();
        }

        ProfileGuidanceContentResult result;

        try
        {
            result = await questionsApiService.GetProfileGuidanceContentAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.GuidanceReportDataLoadFailed(exception, type);
            ErrorMessage = FailureMessage;

            return Page();
        }

        if (result.Outcome == ProfileGuidanceContentOutcome.Forbidden)
        {
            logger.GuidanceReportRefused(type);

            return StatusCode(StatusCodes.Status403Forbidden);
        }

        var producedOn = clock.GetLocalNow();

        try
        {
            var content = result.Content!;
            var html = type == GuidanceReportType.SummaryPrioritisationReport
                ? await viewRenderer.RenderAsync(SprViewPath, SprGuidanceReportLayout.Build(content))
                : await viewRenderer.RenderAsync(GuidanceViewPath, GuidanceReportLayout.Build(type, content));
            var pdf = await pdfService.RenderAsync(html, GuidanceReportLayout.BuildPdfOptions(GuidanceReportLayout.TitleOf(type), producedOn), cancellationToken);

            return File(pdf, "application/pdf", GuidanceReportLayout.BuildFileName(type, producedOn));
        }
        catch (Exception exception) when (exception is PdfRenderException or InvalidOperationException)
        {
            // A question the report prints is missing from the content, or the PDF could not be made.
            logger.GuidanceReportRenderFailed(exception, type);
            ErrorMessage = FailureMessage;

            return Page();
        }
    }

    /// <summary>Reads the report named in the query string: the full report when none is given, and not an unknown name or a number.</summary>
    private bool TryReadType(out GuidanceReportType type)
    {
        type = GuidanceReportType.All;

        if (string.IsNullOrEmpty(Type))
        {
            return true;
        }

        return Enum.TryParse(Type, ignoreCase: true, out type) && Enum.IsDefined(type) && !char.IsDigit(Type[0]);
    }
}
