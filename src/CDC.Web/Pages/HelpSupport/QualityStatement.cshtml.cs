using CDC.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CDC.Web.Pages.HelpSupport;

/// <summary>
/// Redirects to the current "D2R2 Quality Statement" document, served directly by CDC.Api -
/// matches the pattern <see cref="StaticReportsModel.GetDocumentUrl"/> uses for its links.
/// </summary>
[AllowAnonymous]
public class QualityStatementModel : BreadcrumbPageModelBase
{
    /// <summary>Matches the title legacy uploads this report under - see <c>Footer.ascx.vb</c>.</summary>
    private const string ReportTitle = "D2R2 Quality Statement";

    private readonly IApiClient apiClient;
    private readonly string apiBaseUrl;
    private readonly ILogger<QualityStatementModel> logger;

    /// <summary>Gets a value indicating whether the static reports API call failed.</summary>
    public bool HasError { get; private set; }

    /// <summary>Gets the message to show the user when <see cref="HasError"/> is <see langword="true"/>.</summary>
    public string? ErrorMessage { get; private set; }

    public QualityStatementModel(IApiClient apiClient, IOptions<ApiOptions> apiOptions, ILogger<QualityStatementModel> logger)
        : base("D2R2 Quality Statement")
    {
        this.apiClient = apiClient;
        this.logger = logger;
        apiBaseUrl = (apiOptions.Value.BaseUrl ?? string.Empty).TrimEnd('/');
    }

    /// <summary>Redirects to the current version's document, or renders an error banner on failure.</summary>
    /// <param name="cancellationToken">Cancels the request if the client disconnects.</param>
    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Legacy's StaticReportWebHelper.GetDownloadUrl defaults userManual to True, and
            // this report is stored with IsUserManual = 1 in the database, despite the "Help and
            // support" footer listing it separately from "Help using D2R2".
            var currentReports = await apiClient.GetCurrentStaticReportsAsync(isUserManual: true, publicOnly: false, cancellationToken);
            var currentVersion = currentReports.FirstOrDefault(report => report.Title == ReportTitle);

            if (currentVersion is null)
            {
                return NotFound();
            }

            return Redirect($"{apiBaseUrl}/api/static-reports/versions/{currentVersion.Id}/document");
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
}

/// <summary>Source-generated structured log messages for <see cref="QualityStatementModel"/>.</summary>
internal static partial class QualityStatementLog
{
    [LoggerMessage(EventId = 8100, Level = LogLevel.Error, Message = "Failed to load the quality statement from CDC.Api")]
    public static partial void StaticReportLoadFailed(this ILogger logger, Exception exception);
}
