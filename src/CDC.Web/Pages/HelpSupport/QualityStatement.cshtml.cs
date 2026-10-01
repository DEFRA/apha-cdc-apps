using CDC.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Web.Pages.HelpSupport;

/// <summary>
/// Downloads the current "D2R2 Quality Statement" static report from CDC.Api.
/// </summary>
/// <param name="staticReportsApiService">Typed client for the static reports endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class QualityStatementModel(IStaticReportsApiService staticReportsApiService, ILogger<QualityStatementModel> logger)
    : BreadcrumbPageModelBase("D2R2 Quality Statement")
{
    /// <summary>Matches the title legacy uploads this report under - see <c>Footer.ascx.vb</c>.</summary>
    private const string ReportTitle = "D2R2 Quality Statement";

    /// <summary>Gets a value indicating whether the static reports API call failed.</summary>
    public bool HasError { get; private set; }

    /// <summary>Gets the message to show the user when <see cref="HasError"/> is <see langword="true"/>.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Downloads the current version of the report, or renders an error banner on failure.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Legacy's StaticReportWebHelper.GetDownloadUrl defaults userManual to True, and
            // this report is stored with IsUserManual = 1 in the database, despite the "Help and
            // support" footer listing it separately from "Help using D2R2".
            var currentReports = await staticReportsApiService.GetCurrentAsync(isUserManual: true, cancellationToken);
            var currentVersion = currentReports.FirstOrDefault(report => report.Title == ReportTitle);

            if (currentVersion is null)
            {
                return NotFound();
            }

            var data = await staticReportsApiService.GetDataAsync(currentVersion.Id, cancellationToken);

            if (data is null)
            {
                return NotFound();
            }

            return File(data.PdfData, "application/pdf", BuildDownloadFileName(data.Title));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            // HttpRequestException: network/DNS failure or a non-success status code.
            // TaskCanceledException: the resilience handler's own request timeout expired.
            // NotSupportedException: the response was not valid JSON for the expected DTO.
            logger.StaticReportLoadFailed(exception);

            HasError = true;
            ErrorMessage = "We could not load the quality statement. Try again later.";

            return Page();
        }
    }

    /// <summary>
    /// Builds a download file name matching legacy's <c>PdfDownloadHelper.DownloadPdfBytes</c>:
    /// the report title with spaces replaced by underscores, followed by the download timestamp.
    /// </summary>
    private static string BuildDownloadFileName(string title) =>
        $"{title.Replace(' ', '_')}_{DateTime.Now:dd_MM_yyyy_HH_mm_ss}.pdf";
}

/// <summary>Source-generated structured log messages for <see cref="QualityStatementModel"/>.</summary>
internal static partial class QualityStatementLog
{
    [LoggerMessage(EventId = 8100, Level = LogLevel.Error, Message = "Failed to load the quality statement from CDC.Api")]
    public static partial void StaticReportLoadFailed(this ILogger logger, Exception exception);
}


