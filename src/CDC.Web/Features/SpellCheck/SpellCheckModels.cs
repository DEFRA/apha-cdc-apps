namespace CDC.Web.Features.SpellCheck;

/// <summary>The words of a text, which the spell check dialog asks to have checked.</summary>
/// <param name="Words">The words, as written in the text. A repeated word need only be sent once.</param>
public sealed record CheckWordsRequest(IReadOnlyList<string>? Words);

/// <summary>The words of a text that are not in a dictionary.</summary>
/// <param name="Misspelt">The misspelt words, as they were sent.</param>
public sealed record CheckWordsResponse(IReadOnlyList<string> Misspelt);

/// <summary>A misspelt word, which the spell check dialog asks for corrections to.</summary>
/// <param name="Word">The misspelt word.</param>
public sealed record SuggestWordRequest(string? Word);

/// <summary>The corrections to a misspelt word.</summary>
/// <param name="Suggestions">The suggestions, best first.</param>
public sealed record SuggestWordResponse(IReadOnlyList<string> Suggestions);

/// <summary>A word the spell check dialog's "Add Custom" button adds to the custom dictionary.</summary>
/// <param name="Word">The word.</param>
public sealed record AddCustomWordRequest(string? Word);
