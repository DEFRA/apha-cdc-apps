using System.Globalization;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Infrastructure.Reports;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Downloads the print version of a profile's current draft as a PDF: the full profile, or one of its sections. Replaces
/// the legacy <c>ShowProfilePrintVersion.aspx</c>, which streamed a TallPDF document; the report is now a Razor view
/// printed to PDF by Playwright's headless Chromium. Reached from the "Print full profile" link and the "Print" button
/// on the profile section pages, which open it in a new tab; the page itself only shows when the PDF cannot be produced.
/// </summary>
/// <param name="apiClient">Typed client for the profile management endpoints on CDC.Api.</param>
/// <param name="pdfBuilder">Builds the full profile and single-section PDFs.</param>
/// <param name="logger">Structured logger.</param>
/// <param name="timeProvider">Supplies the time stamped into the file name; defaults to the system clock.</param>
public class ViewProfilePrintVersionModel(
    IApiClient apiClient,
    FullProfileReportPdfBuilder pdfBuilder,
    ILogger<ViewProfilePrintVersionModel> logger,
    TimeProvider? timeProvider = null) : PageModel
{
    /// <summary>The path of this page, which the "Print full profile" link and "Print" button open.</summary>
    public const string PagePath = "/SurveillanceProfiles/ViewProfilePrintVersion";

    /// <summary>The message shown when the profile could not be loaded.</summary>
    public const string LoadFailureMessage = "We could not load this profile. Try again later.";

    /// <summary>The message shown when the PDF could not be produced.</summary>
    public const string BuildFailureMessage = "We could not create the print version of this profile. Try again later.";

    // The legacy file name ended "_{Now}", which became "dd_MM_yyyy_HH_mm_ss" once the date and time separators were replaced.
    private const string FileNameTimestampFormat = "dd_MM_yyyy_HH_mm_ss";

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>Gets or sets the profile to print, bound from the page route.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid ProfileId { get; set; }

    /// <summary>Gets or sets the one section to print, bound from the query string; the whole profile is printed when it is missing.</summary>
    [BindProperty(SupportsGet = true)]
    public Guid? Section { get; set; }

    /// <summary>Gets the message shown when the PDF could not be produced, otherwise <see langword="null"/>.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Gets the page's title and heading.</summary>
    public string PageTitle => Section is null ? "Print full profile" : "Print profile section";

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        // The section is optional, so a value that is not a GUID must not quietly print the whole profile.
        if (!ModelState.IsValid)
        {
            return BadRequest();
        }

        ManageProfileViewModel? profile;

        try
        {
            profile = await apiClient.GetManageProfileAsync(ProfileId, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.ProfilePrintVersionLoadFailed(exception, ProfileId);
            ErrorMessage = LoadFailureMessage;

            return Page();
        }

        if (profile is null || profile.CurrentProfileVersionId == Guid.Empty)
        {
            return NotFound();
        }

        // The report content endpoint does not check who is asking, so the legacy CanGetPrintVersion() rule is enforced here.
        if (!profile.CanPrintProfileSection)
        {
            logger.ProfilePrintVersionRefused(ProfileId);

            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return await BuildPrintVersionAsync(profile, cancellationToken);
    }

    private async Task<IActionResult> BuildPrintVersionAsync(ManageProfileViewModel profile, CancellationToken cancellationToken)
    {
        byte[]? pdf;

        try
        {
            pdf = Section is { } sectionId
                ? await pdfBuilder.BuildSectionAsync(profile.CurrentProfileVersionId, sectionId, cancellationToken)
                : await pdfBuilder.BuildAsync(profile.CurrentProfileVersionId, cancellationToken);
        }
        catch (Exception exception) when (exception is PdfRenderException or InvalidOperationException or HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.ProfilePrintVersionBuildFailed(exception, ProfileId, profile.CurrentProfileVersionId);
            ErrorMessage = BuildFailureMessage;

            return Page();
        }

        return pdf is null ? NotFound() : File(pdf, "application/pdf", BuildFileName(profile));
    }

    /// <summary>Builds the legacy "{title}({status})_{version}_Print full profile_{date and time}" file name.</summary>
    private string BuildFileName(ManageProfileViewModel profile)
    {
        var status = profile.CurrentVersionIsPublished ? "Published" : "Draft";
        var printedAt = clock.GetLocalNow().ToString(FileNameTimestampFormat, CultureInfo.InvariantCulture);

        return PdfFileName.Build($"{ProfileTitleHtmlFormatter.ToPlainText(profile.FullTitle)}({status})_{profile.CurrentVersionNumber}_Print full profile_{printedAt}");
    }
}
