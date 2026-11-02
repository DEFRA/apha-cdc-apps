namespace CDC.Web.Infrastructure.SpellCheck;

/// <summary>
/// Source-generated structured log messages for the spell check. Only counts and outcomes are logged - never the words, which
/// users type.
/// </summary>
internal static partial class SpellCheckLog
{
    [LoggerMessage(EventId = 2700, Level = LogLevel.Information, Message = "Spell check found {MisspeltWordCount} misspelt words")]
    public static partial void CheckedWords(this ILogger logger, int misspeltWordCount);

    [LoggerMessage(EventId = 2701, Level = LogLevel.Information, Message = "A word was added to the custom dictionary")]
    public static partial void AddedCustomWord(this ILogger logger);

    [LoggerMessage(EventId = 2702, Level = LogLevel.Warning, Message = "A word was not added to the custom dictionary: {Outcome}")]
    public static partial void AddCustomWordRefused(this ILogger logger, AddCustomWordOutcome outcome);

    [LoggerMessage(EventId = 2703, Level = LogLevel.Warning, Message = "The custom dictionary could not be read, so the spell check ran without it")]
    public static partial void CustomWordsUnavailable(this ILogger logger, Exception exception);
}
