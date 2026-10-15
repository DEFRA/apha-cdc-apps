namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>Source-generated structured log messages for the Create bespoke report page.</summary>
internal static partial class CreateBespokeReportLog
{
    [LoggerMessage(EventId = 2520, Level = LogLevel.Error, Message = "Failed to load the bespoke report page for profile version '{ProfileVersionId}' from CDC.Api")]
    public static partial void BespokeReportLoadFailed(this ILogger logger, Exception exception, Guid profileVersionId);

    [LoggerMessage(EventId = 2521, Level = LogLevel.Error, Message = "A bespoke report template call for profile version '{ProfileVersionId}' failed")]
    public static partial void BespokeTemplateCallFailed(this ILogger logger, Exception exception, Guid profileVersionId);

    [LoggerMessage(EventId = 2522, Level = LogLevel.Error, Message = "Failed to generate the bespoke report for profile version '{ProfileVersionId}'")]
    public static partial void BespokeReportGenerationFailed(this ILogger logger, Exception exception, Guid profileVersionId);

    [LoggerMessage(EventId = 2523, Level = LogLevel.Warning, Message = "CDC.Api refused a bespoke report template request for profile version '{ProfileVersionId}' with outcome {Outcome}")]
    public static partial void BespokeTemplateRequestRefused(this ILogger logger, Guid profileVersionId, CDC.Web.Models.BespokeTemplatesOutcome outcome);
}
