namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>Source-generated structured log messages for the draft summary report downloads. Only identifiers are logged.</summary>
internal static partial class DraftSummaryReportLog
{
    [LoggerMessage(EventId = 2580, Level = LogLevel.Error, Message = "Failed to load profile '{ProfileId}' from CDC.Api to run its {Report} report")]
    public static partial void DraftSummaryReportLoadFailed(this ILogger logger, Exception exception, Guid profileId, CDC.Web.Models.DraftSummaryReport report);

    [LoggerMessage(EventId = 2581, Level = LogLevel.Error, Message = "Failed to build the {Report} report of profile '{ProfileId}' (profile version '{ProfileVersionId}')")]
    public static partial void DraftSummaryReportBuildFailed(this ILogger logger, Exception exception, CDC.Web.Models.DraftSummaryReport report, Guid profileId, Guid profileVersionId);

    [LoggerMessage(EventId = 2582, Level = LogLevel.Warning, Message = "The {Report} report of profile '{ProfileId}' was refused because the user may not run it on the current version")]
    public static partial void DraftSummaryReportRefused(this ILogger logger, CDC.Web.Models.DraftSummaryReport report, Guid profileId);

    [LoggerMessage(EventId = 2583, Level = LogLevel.Warning, Message = "CDC.Api did not prioritise profile version '{ProfileVersionId}' for the summary prioritisation report: {Outcome}")]
    public static partial void DraftSummaryReportPrioritiseRefused(this ILogger logger, Guid profileVersionId, CDC.Web.Models.ProfileReportsOutcome outcome);

    [LoggerMessage(EventId = 2584, Level = LogLevel.Warning, Message = "Rejected the unknown draft summary report '{RequestedReport}'")]
    public static partial void DraftSummaryReportRejected(this ILogger logger, string requestedReport);
}
