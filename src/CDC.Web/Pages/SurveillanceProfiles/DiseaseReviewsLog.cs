namespace CDC.Web.Pages.SurveillanceProfiles;

/// <summary>Source-generated structured log messages for the disease reviews report download.</summary>
internal static partial class DiseaseReviewsLog
{
    [LoggerMessage(EventId = 2550, Level = LogLevel.Error, Message = "Failed to load the list of authors for the disease reviews report from CDC.Api")]
    public static partial void DiseaseReviewsAuthorsLoadFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 2551, Level = LogLevel.Error, Message = "Failed to load the disease reviews report data for author '{AuthorId}' from CDC.Api")]
    public static partial void DiseaseReviewsDataLoadFailed(this ILogger logger, Exception exception, Guid authorId);

    [LoggerMessage(EventId = 2552, Level = LogLevel.Error, Message = "Failed to render the disease reviews report PDF for author '{AuthorId}'")]
    public static partial void DiseaseReviewsRenderFailed(this ILogger logger, Exception exception, Guid authorId);

    [LoggerMessage(EventId = 2553, Level = LogLevel.Warning, Message = "CDC.Api refused the disease reviews report for author '{AuthorId}' with outcome {Outcome}")]
    public static partial void DiseaseReviewsRefused(this ILogger logger, Guid authorId, CDC.Web.Models.DiseaseReviewsReportOutcome outcome);
}
