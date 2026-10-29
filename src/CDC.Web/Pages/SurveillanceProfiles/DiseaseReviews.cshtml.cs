using System.Globalization;
using CDC.Web.Authorization.Constants;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>
/// Downloads the disease reviews report as a PDF: the profile sections whose author, VA or policy review falls
/// due between two dates for one user. Replaces the legacy <c>DiseaseReviews.aspx</c>, which streamed a TallPDF
/// document; the report is now a Razor view printed to PDF by Playwright's headless Chromium. Reached from the
/// "Review Timings" link in the header menu.
/// </summary>
/// <param name="globalUsersApiService">Typed client for the global user endpoints on CDC.Api, which list the authors to choose from.</param>
/// <param name="reportsApiService">Typed client for the profile report endpoints on CDC.Api.</param>
/// <param name="viewRenderer">Renders the report's Razor view to HTML.</param>
/// <param name="pdfService">Prints the HTML to PDF.</param>
/// <param name="logger">Structured logger.</param>
/// <param name="timeProvider">Supplies the time the report is produced; defaults to the system clock.</param>
[Authorize(Policy = AuthorizationPolicies.CanViewReviewTiming)]
public class DiseaseReviewsModel(
    IGlobalUsersApiService globalUsersApiService,
    IProfileReportsApiService reportsApiService,
    IRazorViewRenderer viewRenderer,
    IPlaywrightPdfService pdfService,
    ILogger<DiseaseReviewsModel> logger,
    TimeProvider? timeProvider = null) : BreadcrumbPageModelBase("Review timings")
{
    /// <summary>The Razor view that lays the report out.</summary>
    public const string ReportViewPath = "/Views/Pdf/DiseaseReviewsReport.cshtml";

    /// <summary>The message shown when the authors could not be listed.</summary>
    public const string AuthorsLoadFailureMessage = "We could not load the list of authors. Try again later.";

    /// <summary>The message shown when the report could not be produced.</summary>
    public const string ReportFailureMessage = "We could not create the disease reviews report. Try again later.";

    /// <summary>The longest description accepted, which is as much as the legacy rich text editor let a user type.</summary>
    public const int MaxDescriptionLength = 255;

    // The review date columns cannot hold a date outside this range, so the report cannot be run for one.
    private static readonly string SupportedDateRange =
        $"{ProfileRevisionDatesForm.MinimumDate.ToString(ProfileRevisionDatesForm.DateFormat, CultureInfo.InvariantCulture)} " +
        $"and {ProfileRevisionDatesForm.MaximumDate.ToString(ProfileRevisionDatesForm.DateFormat, CultureInfo.InvariantCulture)}";

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <summary>Gets or sets the first day of the range, as typed.</summary>
    [BindProperty]
    public string? FromDate { get; set; }

    /// <summary>Gets or sets the last day of the range, as typed.</summary>
    [BindProperty]
    public string? ToDate { get; set; }

    /// <summary>Gets or sets the user whose reviews are due.</summary>
    [BindProperty]
    public Guid? AuthorId { get; set; }

    /// <summary>Gets or sets the description printed at the top of the report, which may hold italic markup.</summary>
    [BindProperty]
    public string? Description { get; set; }

    /// <summary>Gets the users the report can be run for.</summary>
    public IReadOnlyList<GlobalUserDto> Authors { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAuthorsAsync(cancellationToken);

        // The legacy list opened with its first author selected.
        AuthorId = Authors.Count > 0 ? Authors[0].Id : null;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        // Every field is checked below, so the binder's own messages for them are not wanted.
        ModelState.Clear();

        if (!await LoadAuthorsAsync(cancellationToken))
        {
            return Page();
        }

        var fromDate = ReadDate(FromDate, nameof(FromDate), "from date");
        var toDate = ReadDate(ToDate, nameof(ToDate), "to date");
        var author = Authors.FirstOrDefault(user => user.Id == AuthorId);
        var description = Description?.Trim() ?? string.Empty;

        if (author is null)
        {
            ModelState.AddModelError(nameof(AuthorId), "Select an author");
        }

        if (description.Length > MaxDescriptionLength)
        {
            ModelState.AddModelError(nameof(Description), $"Description must be {MaxDescriptionLength} characters or fewer");
        }

        if (!ModelState.IsValid || fromDate is not { } from || toDate is not { } to || author is null)
        {
            return Page();
        }

        return await BuildReportAsync(from, to, author, description, cancellationToken);
    }

    private async Task<IActionResult> BuildReportAsync(
        DateTime fromDate,
        DateTime toDate,
        GlobalUserDto author,
        string description,
        CancellationToken cancellationToken)
    {
        var result = await LoadReportAsync(fromDate, toDate, author.Id, cancellationToken);

        switch (result.Outcome)
        {
            case DiseaseReviewsReportOutcome.Forbidden:
                logger.DiseaseReviewsRefused(author.Id, result.Outcome);
                return StatusCode(StatusCodes.Status403Forbidden);
            case DiseaseReviewsReportOutcome.Error:
                ModelState.AddModelError(string.Empty, ReportFailureMessage);
                return Page();
        }

        var layout = DiseaseReviewsReportLayout.Build(result.Report!, description, author.FullName, fromDate, toDate);
        var producedOn = clock.GetLocalNow();

        try
        {
            var html = await viewRenderer.RenderAsync(ReportViewPath, layout);
            var pdf = await pdfService.RenderAsync(
                html,
                DiseaseReviewsReportLayout.BuildPdfOptions(author.FullName, fromDate, toDate, producedOn),
                cancellationToken);

            return File(pdf, "application/pdf", DiseaseReviewsReportLayout.BuildFileName(producedOn));
        }
        catch (Exception exception) when (exception is PdfRenderException or InvalidOperationException)
        {
            logger.DiseaseReviewsRenderFailed(exception, author.Id);
            ModelState.AddModelError(string.Empty, ReportFailureMessage);

            return Page();
        }
    }

    /// <summary>Reads a typed date, or records why it cannot be used.</summary>
    private DateTime? ReadDate(string? text, string field, string label)
    {
        var value = text?.Trim() ?? string.Empty;

        if (value.Length == 0)
        {
            ModelState.AddModelError(field, $"Enter a {label}");

            return null;
        }

        if (!ProfileRevisionDatesForm.TryParseDate(value, out var date))
        {
            ModelState.AddModelError(field, $"The {label} must be a real date, for example 1 January 2027");

            return null;
        }

        if (date.Date < ProfileRevisionDatesForm.MinimumDate || date.Date > ProfileRevisionDatesForm.MaximumDate)
        {
            ModelState.AddModelError(field, $"The {label} must be between {SupportedDateRange}");

            return null;
        }

        return date.Date;
    }

    private async Task<bool> LoadAuthorsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // A page size of 0 asks for every user on one page.
            Authors = (await globalUsersApiService.GetGlobalUsersAsync(1, 0, cancellationToken)).Items;

            return true;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.DiseaseReviewsAuthorsLoadFailed(exception);
            ModelState.AddModelError(string.Empty, AuthorsLoadFailureMessage);

            return false;
        }
    }

    private async Task<DiseaseReviewsReportResult> LoadReportAsync(
        DateTime fromDate,
        DateTime toDate,
        Guid authorId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await reportsApiService.GetDiseaseReviewsReportAsync(fromDate, toDate, authorId, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.DiseaseReviewsDataLoadFailed(exception, authorId);

            return new DiseaseReviewsReportResult(DiseaseReviewsReportOutcome.Error);
        }
    }
}
