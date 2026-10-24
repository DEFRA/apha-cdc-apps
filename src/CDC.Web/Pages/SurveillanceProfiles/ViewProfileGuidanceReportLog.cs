namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>Source-generated structured log messages for the guidance report download.</summary>
internal static partial class ViewProfileGuidanceReportLog
{
    [LoggerMessage(EventId = 2570, Level = LogLevel.Warning, Message = "CDC.Api refused the {ReportType} guidance report")]
    public static partial void GuidanceReportRefused(this ILogger logger, CDC.Web.Models.GuidanceReportType reportType);

    [LoggerMessage(EventId = 2571, Level = LogLevel.Error, Message = "Failed to load the guidance for the {ReportType} guidance report from CDC.Api")]
    public static partial void GuidanceReportDataLoadFailed(this ILogger logger, Exception exception, CDC.Web.Models.GuidanceReportType reportType);

    [LoggerMessage(EventId = 2572, Level = LogLevel.Error, Message = "Failed to render the {ReportType} guidance report PDF")]
    public static partial void GuidanceReportRenderFailed(this ILogger logger, Exception exception, CDC.Web.Models.GuidanceReportType reportType);

    [LoggerMessage(EventId = 2573, Level = LogLevel.Warning, Message = "Rejected the unknown guidance report type '{RequestedType}'")]
    public static partial void GuidanceReportTypeRejected(this ILogger logger, string requestedType);
}
