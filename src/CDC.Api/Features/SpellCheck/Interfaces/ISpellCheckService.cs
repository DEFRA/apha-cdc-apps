using CDC.Api.Features.SpellCheck.Dtos;

namespace CDC.Api.Features.SpellCheck.Interfaces;

/// <summary>
/// Reads and maintains the spell check's custom dictionary, the words every user's spell check accepts in addition to the
/// English dictionary. Mirrors the legacy <c>ProfilesLibrary.CustomWordList</c> business object behind <c>CustomWordSource</c>.
/// </summary>
public interface ISpellCheckService
{
    /// <summary>Gets every word in the custom dictionary.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The words, in no particular order.</returns>
    /// <exception cref="UnauthorizedAccessException">The request names no known user.</exception>
    Task<IReadOnlyList<string>> GetCustomWordsAsync(CancellationToken cancellationToken);

    /// <summary>Adds a word to the custom dictionary. A word that is there already, whatever its case, is not added again.</summary>
    /// <param name="word">The word to add.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the word is now in the dictionary, or why it is not.</returns>
    Task<AddCustomWordResult> AddCustomWordAsync(string? word, CancellationToken cancellationToken);
}
