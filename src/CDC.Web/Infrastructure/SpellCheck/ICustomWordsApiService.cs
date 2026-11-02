namespace CDC.Web.Infrastructure.SpellCheck;

/// <summary>
/// Reads and adds to the spell check's custom dictionary, which CDC.Api keeps in the database. Replaces the legacy
/// <c>SpellCheckService.svc</c> WCF client.
/// </summary>
public interface ICustomWordsApiService
{
    /// <summary>Gets every word in the custom dictionary.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The words, in no particular order.</returns>
    Task<IReadOnlyList<string>> GetCustomWordsAsync(CancellationToken cancellationToken = default);

    /// <summary>Adds a word to the custom dictionary.</summary>
    /// <param name="word">The word to add.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the word is now in the dictionary, or why it is not.</returns>
    Task<AddCustomWordOutcome> AddCustomWordAsync(string word, CancellationToken cancellationToken = default);
}

/// <summary>Outcome of <see cref="ICustomWordsApiService.AddCustomWordAsync"/>.</summary>
public enum AddCustomWordOutcome
{
    /// <summary>The word is in the custom dictionary: it was added, or was there already.</summary>
    Added,

    /// <summary>The user is not permitted to add words to the custom dictionary.</summary>
    Forbidden,

    /// <summary>CDC.Api refused the word: it is empty, too long, or more than one word.</summary>
    Invalid,

    /// <summary>The word could not be added for another reason.</summary>
    Failed
}
