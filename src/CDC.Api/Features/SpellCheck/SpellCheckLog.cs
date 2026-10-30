namespace CDC.Api.Features.SpellCheck;

/// <summary>
/// Source-generated structured log messages for the spell check feature. Only counts and identifiers are logged - never the
/// words, which users type.
/// </summary>
internal static partial class SpellCheckLog
{
    [LoggerMessage(EventId = 9200, Level = LogLevel.Information, Message = "Retrieved {WordCount} words from the custom dictionary")]
    public static partial void RetrievedCustomWords(this ILogger logger, int wordCount);

    [LoggerMessage(EventId = 9201, Level = LogLevel.Information, Message = "Added custom dictionary word {WordId}")]
    public static partial void AddedCustomWord(this ILogger logger, Guid wordId);

    [LoggerMessage(EventId = 9202, Level = LogLevel.Information, Message = "A word to add to the custom dictionary was already there")]
    public static partial void CustomWordAlreadyExists(this ILogger logger);

    [LoggerMessage(EventId = 9203, Level = LogLevel.Warning, Message = "Adding a word to the custom dictionary was denied: the current user is not a profile editor, contributor or reviewer")]
    public static partial void AddCustomWordForbidden(this ILogger logger);

    [LoggerMessage(EventId = 9204, Level = LogLevel.Error, Message = "Stored procedure {StoredProcedure} failed")]
    public static partial void StoredProcedureFailed(this ILogger logger, Exception exception, string storedProcedure);
}
