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

    [LoggerMessage(EventId = 6005, Level = LogLevel.Information, Message = "Retrieved the contributions report for profile {ProfileId} covering {VersionCount} draft versions")]
    public static partial void RetrievedContributionsReport(this ILogger logger, Guid profileId, int versionCount);

    [LoggerMessage(EventId = 6006, Level = LogLevel.Warning, Message = "The contributions report for profile {ProfileId} was refused: the user may not run it")]
    public static partial void ContributionsReportRefused(this ILogger logger, Guid profileId);

    [LoggerMessage(EventId = 6007, Level = LogLevel.Information, Message = "Retrieved the profile reports overview for profile version {ProfileVersionId} with {ReportCount} reports")]
    public static partial void RetrievedProfileReportsOverview(this ILogger logger, Guid profileVersionId, int reportCount);

    [LoggerMessage(EventId = 6008, Level = LogLevel.Warning, Message = "Persisting a report for profile version {ProfileVersionId} was refused: the user may not create reports")]
    public static partial void CreateProfileReportRefused(this ILogger logger, Guid profileVersionId);

    [LoggerMessage(EventId = 6009, Level = LogLevel.Information, Message = "Built the report content for profile version {ProfileVersionId} with {SectionCount} sections")]
    public static partial void BuiltProfileReportContent(this ILogger logger, Guid profileVersionId, int sectionCount);

    [LoggerMessage(EventId = 6010, Level = LogLevel.Information, Message = "Read the prioritisation scores for profile version {ProfileVersionId}: {CategoryScoreCount} category scores and {CriterionScoreCount} criterion scores")]
    public static partial void BuiltProfilePrioritisationScores(this ILogger logger, Guid profileVersionId, int categoryScoreCount, int criterionScoreCount);

    [LoggerMessage(EventId = 6011, Level = LogLevel.Information, Message = "Read the guidance of {FoundCount} of {RequestedCount} requested questions")]
    public static partial void ReadQuestionGuidance(this ILogger logger, int requestedCount, int foundCount);

    [LoggerMessage(EventId = 6012, Level = LogLevel.Information, Message = "Retrieved the disease reviews report for author {AuthorId} with {SectionCount} sections due")]
    public static partial void RetrievedDiseaseReviewsReport(this ILogger logger, Guid authorId, int sectionCount);

    [LoggerMessage(EventId = 6013, Level = LogLevel.Warning, Message = "The disease reviews report was refused: the user may not run it")]
    public static partial void DiseaseReviewsReportRefused(this ILogger logger);

    [LoggerMessage(EventId = 6014, Level = LogLevel.Information, Message = "Recalculated the prioritisation scores for profile version {ProfileVersionId}")]
    public static partial void PrioritisedProfileVersion(this ILogger logger, Guid profileVersionId);

    [LoggerMessage(EventId = 6015, Level = LogLevel.Warning, Message = "Prioritising profile version {ProfileVersionId} was refused: the user may not prioritise it")]
    public static partial void PrioritiseProfileVersionRefused(this ILogger logger, Guid profileVersionId);
}
