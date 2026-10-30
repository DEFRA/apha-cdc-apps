namespace CDC.Web.Infrastructure.SpellCheck;

/// <summary>
/// The English dictionary the spell check looks words up in: the equivalent of the legacy RadSpell <c>en-US.TDF</c> dictionary.
/// </summary>
public interface ISpellingWordList
{
    /// <summary>Gets whether a word is spelt correctly.</summary>
    /// <param name="word">A single word, with no surrounding punctuation.</param>
    /// <returns><see langword="true"/> when the dictionary knows the word.</returns>
    bool Check(string word);

    /// <summary>Gets the words the dictionary suggests in place of a misspelt one, best first.</summary>
    /// <param name="word">The misspelt word.</param>
    /// <returns>The suggestions, which may be none.</returns>
    IReadOnlyList<string> Suggest(string word);
}
