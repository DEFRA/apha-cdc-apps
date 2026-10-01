namespace CDC.Api.Features.ProfileQuestions;

/// <summary>
/// Source-generated structured log messages for the profile questions feature. Nothing here
/// records personal data: only identifiers are logged.
/// </summary>
internal static partial class ProfileQuestionLog
{
    [LoggerMessage(EventId = 5000, Level = LogLevel.Information, Message = "Retrieved profile question {ProfileQuestionId}")]
    public static partial void RetrievedProfileQuestion(this ILogger logger, Guid profileQuestionId);

    [LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "No profile question found for {ProfileQuestionId}")]
    public static partial void ProfileQuestionNotFound(this ILogger logger, Guid profileQuestionId);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Information, Message = "Retrieved {QuestionCount} questions for profile section {ProfileSectionId}")]
    public static partial void RetrievedProfileQuestionInfoList(this ILogger logger, int questionCount, Guid profileSectionId);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Information, Message = "Updated profile question {ProfileQuestionId}")]
    public static partial void UpdatedProfileQuestion(this ILogger logger, Guid profileQuestionId);

    [LoggerMessage(EventId = 5004, Level = LogLevel.Warning, Message = "Rejected a concurrent update to profile question {ProfileQuestionId}")]
    public static partial void ConcurrencyConflict(this ILogger logger, Guid profileQuestionId);

    [LoggerMessage(EventId = 5005, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);
}
