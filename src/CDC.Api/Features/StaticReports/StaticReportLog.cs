namespace CDC.Api.Features.StaticReports;

/// <summary>
/// Source-generated structured log messages for the static reports feature. Nothing here
/// records personal data: only identifiers, titles and counts are logged.
/// </summary>
internal static partial class StaticReportLog
{
    [LoggerMessage(EventId = 7000, Level = LogLevel.Information, Message = "Retrieved {ReportCount} current static reports (IsUserManual={IsUserManual})")]
    public static partial void RetrievedCurrentStaticReports(this ILogger logger, int reportCount, bool isUserManual);

    [LoggerMessage(EventId = 7001, Level = LogLevel.Information, Message = "Retrieved {VersionCount} history versions for static report {StaticReportId}")]
    public static partial void RetrievedStaticReportHistory(this ILogger logger, int versionCount, Guid staticReportId);

    [LoggerMessage(EventId = 7002, Level = LogLevel.Information, Message = "Retrieved data for static report version {StaticReportVersionId}")]
    public static partial void RetrievedStaticReportData(this ILogger logger, Guid staticReportVersionId);

    [LoggerMessage(EventId = 7003, Level = LogLevel.Information, Message = "No static report version found with id {StaticReportVersionId}")]
    public static partial void StaticReportDataNotFound(this ILogger logger, Guid staticReportVersionId);

    [LoggerMessage(EventId = 7004, Level = LogLevel.Information, Message = "Uploaded static report '{Title}' (IsUserManual={IsUserManual}, IsPublic={IsPublic})")]
    public static partial void UploadedStaticReport(this ILogger logger, string title, bool isUserManual, bool isPublic);

    [LoggerMessage(EventId = 7005, Level = LogLevel.Information, Message = "Deleted static report version {StaticReportVersionId}")]
    public static partial void DeletedStaticReportVersion(this ILogger logger, Guid staticReportVersionId);

    [LoggerMessage(EventId = 7006, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);
}
