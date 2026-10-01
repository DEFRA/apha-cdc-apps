using System.Globalization;
using CDC.Web.Infrastructure;

namespace CDC.Web.Pages.HelpSupport;

/// <summary>
/// Previous versions of one "Help using D2R2" document. Replaces the legacy
/// <c>StaticReports.aspx?StaticReportId=...</c> history mode.
/// </summary>
/// <param name="staticReportsApiService">Typed client for the static reports endpoints on CDC.Api.</param>
/// <param name="logger">Structured logger.</param>
public class DocumentHistoryModel(IStaticReportsApiService staticReportsApiService, ILogger<DocumentHistoryModel> logger)
    : BreadcrumbPageModelBase("Help using D2R2")
{
    /// <summary>Gets every version of the document, most recent first.</summary>
    public IReadOnlyList<DocumentHistoryRow> Versions { get; private set; } = [];

    /// <summary>Gets a value indicating whether the history could not be loaded.</summary>
    public bool HasError { get; private set; }

    public async Task OnGetAsync(Guid staticReportId, CancellationToken cancellationToken)
    {
        try
        {
            var history = await staticReportsApiService.GetHistoryAsync(staticReportId, cancellationToken);

            Versions =
            [
                .. history
                    .OrderByDescending(version => version.VersionMajor)
                    .Select(version => new DocumentHistoryRow(
                        version.Title,
                        $"{version.VersionMajor}.0",
                        version.EffectiveDateFrom.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)))
            ];
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException)
        {
            logger.DocumentHistoryLoadFailed(exception, staticReportId);

            HasError = true;
        }
    }
}

public sealed record DocumentHistoryRow(string Title, string Version, string EffectiveDate);

/// <summary>Source-generated structured log messages for <see cref="DocumentHistoryModel"/>.</summary>
internal static partial class DocumentHistoryLog
{
    [LoggerMessage(EventId = 3010, Level = LogLevel.Error, Message = "Failed to load history for static report {StaticReportId} from CDC.Api")]
    public static partial void DocumentHistoryLoadFailed(this ILogger logger, Exception exception, Guid staticReportId);
}
