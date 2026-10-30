using System.Globalization;
using System.Net;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Infrastructure.Reports;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Downloads the summary profile report or the summary prioritisation report of a profile's current draft as a PDF. Replaces the legacy
/// <c>ShowDraftSummaryProfileReport.aspx</c> and <c>ShowDraftSummaryPrioritisationReport.aspx</c>, which streamed a TallPDF document; each
/// report is now a Razor view printed to PDF by Playwright's headless Chromium. As the legacy page did, the prioritisation report first
/// recalculates the version's prioritisation scores, so it never prints stale ones. Reached from the "Summary profile report" and "Summary
/// prioritisation report" links beside the profile, which open it in a new tab; the page itself only shows when the PDF cannot be produced.
/// </summary>
/// <param name="apiClient">Typed client for the profile management endpoints on CDC.Api.</param>
/// <param name="reportsApiService">Typed client for the profile report endpoints on CDC.Api.</param>
/// <param name="reportGenerator">Generates report PDFs by report name.</param>
/// <param name="logger">Structured logger.</param>
/// <param name="timeProvider">Supplies the time stamped into the file name; defaults to the system clock.</param>
public class ViewDraftSummaryReportModel(
    IApiClient apiClient,
    IProfileReportsApiService reportsApiService,
    IProfileReportGenerator reportGenerator,
    ILogger<ViewDraftSummaryReportModel> logger,
    TimeProvider? timeProvider = null) : PageModel
{
    /// <summary>The path of this page, which the report links open.</summary>
    public const string PagePath = "/SurveillanceProfiles/ViewDraftSummaryReport";

    /// <summary>The message shown when the profile could not be loaded.</summary>
    public const string LoadFailureMessage = "We could not load this profile. Try again later.";

    /// <summary>What the Application error page says when CDC.Api refuses to prioritise the version, in the legacy exception's words.</summary>
    public const string PrioritiseRefusedDetails = "You do not have permission to prioritise this profile version";

    // The legacy file name ended "_{Now}", which became "dd_MM_yyyy_HH_mm_ss" once the date and time separators were replaced.
    private const string FileNameTimestampFormat = "dd_MM_yyyy_HH_mm_ss";

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>Gets or sets the profile whose current draft is reported on, bound from the page route.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid ProfileId { get; set; }

    /// <summary>Gets or sets the report to produce, as named in the page route: the name of a <see cref="DraftSummaryReport"/>.</summary>
    [BindProperty(SupportsGet = true)]
    public string? Report { get; set; }

    /// <summary>Gets the message shown when the PDF could not be produced, otherwise <see langword="null"/>.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Gets the report the route names, otherwise <see langword="null"/>.</summary>
    public DraftSummaryReport? ReportType => TryReadReport(Report, out var report) ? report : null;

    /// <summary>Gets the page's title and heading: the name of the report.</summary>
    public string PageTitle => ReportType is { } report ? TitleOf(report) : "Summary report";

    /// <summary>Gets the report's name as the links, the legacy file names and the legacy exception messages wrote it.</summary>
    /// <param name="report">The report.</param>
    /// <returns>The name, in sentence case.</returns>
    public static string TitleOf(DraftSummaryReport report) => report switch
    {
        DraftSummaryReport.SummaryProfile => "Summary profile report",
        _ => "Summary prioritisation report"
    };

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (ReportType is not { } report)
        {
            logger.DraftSummaryReportRejected(Report ?? string.Empty);

            return NotFound();
        }

        ManageProfileViewModel? profile;

        try
        {
            profile = await apiClient.GetManageProfileAsync(ProfileId, cancellationToken);
        }
        catch (HttpRequestException exception) when (exception.StatusCode == HttpStatusCode.Forbidden)
        {
            logger.DraftSummaryReportRefused(report, ProfileId);

            return RedirectToAccessRefusedPage(RefusedDetailsOf(report));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.DraftSummaryReportLoadFailed(exception, ProfileId, report);
            ErrorMessage = LoadFailureMessage;

            return Page();
        }

        if (profile is null || profile.CurrentProfileVersionId == Guid.Empty)
        {
            return NotFound();
        }

        // The legacy CanGetSummaryReport() rule: the current draft, for a profile editor, a policy profile user or someone assigned to it.
        if (!profile.CanViewProfileReports)
        {
            logger.DraftSummaryReportRefused(report, ProfileId);

            return RedirectToAccessRefusedPage(RefusedDetailsOf(report));
        }

        return await BuildReportAsync(report, profile, cancellationToken);
    }

    private static string RefusedDetailsOf(DraftSummaryReport report) =>
        $"You do not have permission to get the {TitleOf(report).ToLowerInvariant()} of this profile version";

    private static string BuildFailureMessageOf(DraftSummaryReport report) =>
        $"We could not create the {TitleOf(report).ToLowerInvariant()}. Try again later.";

    private static string ReportNameOf(DraftSummaryReport report) => report switch
    {
        DraftSummaryReport.SummaryProfile => ProfileReportDocuments.SummaryProfileReportName,
        _ => ProfilePrioritisationDocuments.ReportName
    };

    private static bool TryReadReport(string? value, out DraftSummaryReport report)
    {
        foreach (var candidate in Enum.GetValues<DraftSummaryReport>())
        {
            if (string.Equals(value, candidate.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                report = candidate;

                return true;
            }
        }

        report = default;

        return false;
    }

    private async Task<IActionResult> BuildReportAsync(DraftSummaryReport report, ManageProfileViewModel profile, CancellationToken cancellationToken)
    {
        var profileVersionId = profile.CurrentProfileVersionId;
        byte[] pdf;

        try
        {
            if (report == DraftSummaryReport.SummaryPrioritisation && await PrioritiseAsync(report, profileVersionId, cancellationToken) is { } refusal)
            {
                return refusal;
            }

            pdf = await reportGenerator.GenerateAsync(ReportNameOf(report), profileVersionId, cancellationToken);
        }
        catch (Exception exception) when (exception is PdfRenderException or InvalidOperationException or HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.DraftSummaryReportBuildFailed(exception, report, ProfileId, profileVersionId);
            ErrorMessage = BuildFailureMessageOf(report);

            return Page();
        }

        return File(pdf, "application/pdf", BuildFileName(report, profile));
    }

    /// <summary>Recalculates the version's prioritisation scores, as the legacy page did before it built the report.</summary>
    /// <returns>A result when the report cannot go ahead; <see langword="null"/> when the scores were recalculated.</returns>
    private async Task<IActionResult?> PrioritiseAsync(DraftSummaryReport report, Guid profileVersionId, CancellationToken cancellationToken)
    {
        var outcome = await reportsApiService.PrioritiseProfileVersionAsync(profileVersionId, cancellationToken);

        switch (outcome)
        {
            case ProfileReportsOutcome.Success:
                return null;
            case ProfileReportsOutcome.NotFound:
                return NotFound();
            case ProfileReportsOutcome.Forbidden:
                logger.DraftSummaryReportPrioritiseRefused(profileVersionId, outcome);

                return RedirectToAccessRefusedPage(PrioritiseRefusedDetails);
            default:
                logger.DraftSummaryReportPrioritiseRefused(profileVersionId, outcome);
                ErrorMessage = BuildFailureMessageOf(report);

                return Page();
        }
    }

    /// <summary>Sends the user to the Application error page, as the legacy site did when a <c>SecurityException</c> refused them the report.</summary>
    private IActionResult RedirectToAccessRefusedPage(string details) =>
        ApplicationErrorModel.RedirectFrom(this, ProfileSectionTabPageModel.AccessRefusedErrorType, details);

    /// <summary>Builds the legacy "{title}({status})_{version}_{report name}_{date and time}" file name.</summary>
    private string BuildFileName(DraftSummaryReport report, ManageProfileViewModel profile)
    {
        var status = profile.CurrentVersionIsPublished ? "Published" : "Draft";
        var producedAt = clock.GetLocalNow().ToString(FileNameTimestampFormat, CultureInfo.InvariantCulture);

        return PdfFileName.Build($"{ProfileTitleHtmlFormatter.ToPlainText(profile.FullTitle)}({status})_{profile.CurrentVersionNumber}_{TitleOf(report)}_{producedAt}");
    }
}
