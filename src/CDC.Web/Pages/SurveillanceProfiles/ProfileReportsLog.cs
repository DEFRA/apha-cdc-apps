namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>Source-generated structured log messages for the Profile reports page.</summary>
internal static partial class ProfileReportsLog
{
    [LoggerMessage(EventId = 2510, Level = LogLevel.Error, Message = "Failed to load the profile reports for profile version '{ProfileVersionId}' from CDC.Api")]
    public static partial void ProfileReportsLoadFailed(this ILogger logger, Exception exception, Guid profileVersionId);

    [LoggerMessage(EventId = 2511, Level = LogLevel.Error, Message = "Failed to download report '{ReportId}' for profile version '{ProfileVersionId}'")]
    public static partial void ProfileReportDownloadFailed(this ILogger logger, Exception exception, Guid reportId, Guid profileVersionId);

    [LoggerMessage(EventId = 2512, Level = LogLevel.Error, Message = "Failed to regenerate report '{ReportName}' for profile version '{ProfileVersionId}'")]
    public static partial void ProfileReportRegenerationFailed(this ILogger logger, Exception exception, string reportName, Guid profileVersionId);

    [LoggerMessage(EventId = 2513, Level = LogLevel.Warning, Message = "Storing regenerated report '{ReportName}' for profile version '{ProfileVersionId}' was refused with outcome {Outcome}")]
    public static partial void ProfileReportStoreRefused(this ILogger logger, string reportName, Guid profileVersionId, CDC.Web.Models.ProfileReportsOutcome outcome);
}
