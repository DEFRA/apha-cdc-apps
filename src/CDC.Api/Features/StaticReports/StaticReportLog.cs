namespace CDC.Api.Features.StaticReports;

/// <summary>
/// Source-generated structured log messages for the static reports feature. Only identifiers and
/// counts are logged - never document contents.
/// </summary>
internal static partial class StaticReportLog
{
    [LoggerMessage(EventId = 8000, Level = LogLevel.Information, Message = "Retrieved {ReportCount} current static reports (user manuals: {IsUserManual})")]
    public static partial void RetrievedCurrentStaticReports(this ILogger logger, int reportCount, bool isUserManual);

    [LoggerMessage(EventId = 8001, Level = LogLevel.Information, Message = "Retrieved {VersionCount} versions for static report {StaticReportId}")]
    public static partial void RetrievedStaticReportHistory(this ILogger logger, int versionCount, Guid staticReportId);

    [LoggerMessage(EventId = 8002, Level = LogLevel.Information, Message = "Deleted static report version {StaticReportVersionId}")]
    public static partial void DeletedStaticReportVersion(this ILogger logger, Guid staticReportVersionId);

    [LoggerMessage(EventId = 8003, Level = LogLevel.Warning, Message = "No document found for static report version {StaticReportVersionId}")]
    public static partial void StaticReportVersionNotFound(this ILogger logger, Guid staticReportVersionId);

    [LoggerMessage(EventId = 8004, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);
}
