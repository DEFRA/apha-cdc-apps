namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>Source-generated structured log messages for the contributions report download.</summary>
internal static partial class ContributionsReportLog
{
    [LoggerMessage(EventId = 2500, Level = LogLevel.Error, Message = "Failed to load the contributions report data for profile '{ProfileId}' from CDC.Api")]
    public static partial void ContributionsReportDataLoadFailed(this ILogger logger, Exception exception, Guid profileId);

    [LoggerMessage(EventId = 2501, Level = LogLevel.Error, Message = "Failed to render the contributions report PDF for profile '{ProfileId}'")]
    public static partial void ContributionsReportRenderFailed(this ILogger logger, Exception exception, Guid profileId);

    [LoggerMessage(EventId = 2502, Level = LogLevel.Warning, Message = "CDC.Api refused the contributions report for profile '{ProfileId}' with outcome {Outcome}")]
    public static partial void ContributionsReportRefused(this ILogger logger, Guid profileId, CDC.Web.Models.ContributionsReportOutcome outcome);
}
