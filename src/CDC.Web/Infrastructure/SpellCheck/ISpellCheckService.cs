namespace CDC.Web.Infrastructure.SpellCheck;

/// <summary>
/// The spell check behind the "Spell check" dialog (<c>wwwroot/js/spell-check.js</c>): finds the misspelt words in a text's words,
/// suggests corrections for one, and adds a word to the custom dictionary. Replaces the legacy RadSpell handler
/// (<c>Telerik.RadSpellCheckHandler.ashx</c>) and its <c>CustomWordSource</c>.
/// </summary>
public interface ISpellCheckService
{
    /// <summary>Finds the words that are neither in the English dictionary nor in the custom dictionary.</summary>
    /// <param name="words">The words of the text, as written. A repeated word is reported once.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The misspelt words, as given, in the order given.</returns>
    Task<IReadOnlyList<string>> FindMisspeltWordsAsync(IEnumerable<string> words, CancellationToken cancellationToken);

    /// <summary>Suggests corrections for a misspelt word: words from the custom dictionary that it is close to, then the English dictionary's.</summary>
    /// <param name="word">The misspelt word.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The suggestions, best first; none when there is nothing to suggest.</returns>
    Task<IReadOnlyList<string>> SuggestAsync(string word, CancellationToken cancellationToken);

    /// <summary>Adds a word to the custom dictionary, which every user's spell check then accepts.</summary>
    /// <param name="word">The word to add.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the word is now in the dictionary, or why it is not.</returns>
    Task<AddCustomWordOutcome> AddCustomWordAsync(string word, CancellationToken cancellationToken);
}
