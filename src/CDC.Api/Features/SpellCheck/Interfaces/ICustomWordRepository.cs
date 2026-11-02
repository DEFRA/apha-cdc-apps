namespace CDC.Api.Features.SpellCheck.Interfaces;

/// <summary>
/// Data access for the spell check's custom dictionary. Mirrors the legacy <c>Profiles.DataAccess.Sql.SpellCheckService</c>.
/// </summary>
public interface ICustomWordRepository
{
    /// <summary>Reads every word in the custom dictionary.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<IReadOnlyList<string>> GetCustomWordsAsync(CancellationToken cancellationToken);

    /// <summary>Adds a word to the custom dictionary.</summary>
    /// <param name="id">The identifier of the new word.</param>
    /// <param name="word">The word.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task AddCustomWordAsync(Guid id, string word, CancellationToken cancellationToken);
}
