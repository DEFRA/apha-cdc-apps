namespace CDC.Api.Features.ProfileReports;

/// <summary>
/// Source-generated structured log messages for the profile reports feature. Nothing here
/// records personal data: only identifiers and counts are logged.
/// </summary>
internal static partial class ProfileReportLog
{
    [LoggerMessage(EventId = 6000, Level = LogLevel.Information, Message = "Retrieved {ReportCount} reports for profile version {ProfileVersionId}")]
    public static partial void RetrievedProfileVersionReports(this ILogger logger, int reportCount, Guid profileVersionId);

    [LoggerMessage(EventId = 6001, Level = LogLevel.Information, Message = "Retrieved report data for report {ProfileReportId} on profile version {ProfileVersionId}")]
    public static partial void RetrievedProfileReportData(this ILogger logger, Guid profileReportId, Guid profileVersionId);

    [LoggerMessage(EventId = 6002, Level = LogLevel.Information, Message = "No report data found for report {ProfileReportId} on profile version {ProfileVersionId}")]
    public static partial void ProfileReportDataNotFound(this ILogger logger, Guid profileReportId, Guid profileVersionId);

    [LoggerMessage(EventId = 6003, Level = LogLevel.Information, Message = "Persisted report {ProfileReportId} for profile version {ProfileVersionId}")]
    public static partial void CreatedProfileReport(this ILogger logger, Guid profileReportId, Guid profileVersionId);

    [LoggerMessage(EventId = 6004, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);
}
