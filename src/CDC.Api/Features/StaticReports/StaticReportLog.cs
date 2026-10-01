namespace CDC.Api.Features.StaticReports;

/// <summary>
/// Source-generated structured log messages for the static reports feature. Nothing here
/// records personal data: only identifiers, titles and counts are logged.
/// </summary>
internal static partial class StaticReportLog
{
    [LoggerMessage(EventId = 8000, Level = LogLevel.Information, Message = "Retrieved {ReportCount} current static reports (user manuals: {IsUserManual})")]
    public static partial void RetrievedCurrentReports(this ILogger logger, int reportCount, bool isUserManual);

    [LoggerMessage(EventId = 8001, Level = LogLevel.Information, Message = "Retrieved report data for static report version {StaticReportVersionId}")]
    public static partial void RetrievedReportData(this ILogger logger, Guid staticReportVersionId);

    [LoggerMessage(EventId = 8002, Level = LogLevel.Information, Message = "No static report version found for {StaticReportVersionId}")]
    public static partial void ReportDataNotFound(this ILogger logger, Guid staticReportVersionId);

    [LoggerMessage(EventId = 8003, Level = LogLevel.Information, Message = "Uploaded a new version of static report '{Title}' (user manual: {IsUserManual})")]
    public static partial void UploadedReport(this ILogger logger, string title, bool isUserManual);

    [LoggerMessage(EventId = 8004, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);
}
