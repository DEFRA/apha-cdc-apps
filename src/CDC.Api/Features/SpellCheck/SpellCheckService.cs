using CDC.Api.Application;
using CDC.Api.Features.SpellCheck.Dtos;
using CDC.Api.Features.SpellCheck.Interfaces;

namespace CDC.Api.Features.SpellCheck;

/// <summary>
/// Default <see cref="ISpellCheckService"/>. Applies the rules the legacy <c>CustomWordList</c> applied in its business
/// layer: every known user can read the custom dictionary, but only a profile editor, or a contributor or reviewer of a profile,
/// can add to it, since a word added there is accepted by every user's spell check.
/// </summary>
/// <param name="repository">Custom dictionary data access.</param>
/// <param name="userContext">The acting user.</param>
/// <param name="logger">Structured logger.</param>
public sealed class SpellCheckService(ICustomWordRepository repository, IUserContext userContext, ILogger<SpellCheckService> logger)
    : ISpellCheckService
{
    /// <summary>The longest word the <c>SpellCustomWord</c> table can hold.</summary>
    internal const int MaxWordLength = 255;

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetCustomWordsAsync(CancellationToken cancellationToken)
    {
        if (!ProfileAccess.CanViewCustomWords(userContext))
        {
            throw new UnauthorizedAccessException("You do not have permission to view the words in the custom dictionary.");
        }

        var words = await repository.GetCustomWordsAsync(cancellationToken);

        logger.RetrievedCustomWords(words.Count);

        return words;
    }

    /// <inheritdoc />
    public async Task<AddCustomWordResult> AddCustomWordAsync(string? word, CancellationToken cancellationToken)
    {
        if (!ProfileAccess.CanEditCustomWords(userContext))
        {
            logger.AddCustomWordForbidden();

            return new AddCustomWordResult(AddCustomWordOutcome.Forbidden, "You do not have permission to add words to the custom dictionary.");
        }

        var trimmed = word?.Trim() ?? string.Empty;
        var error = Validate(trimmed);

        if (error is not null)
        {
            return new AddCustomWordResult(AddCustomWordOutcome.ValidationFailed, error);
        }

        var existing = await repository.GetCustomWordsAsync(cancellationToken);

        if (existing.Contains(trimmed, StringComparer.OrdinalIgnoreCase))
        {
            logger.CustomWordAlreadyExists();

            return new AddCustomWordResult(AddCustomWordOutcome.Success, null);
        }

        var id = Guid.NewGuid();
        await repository.AddCustomWordAsync(id, trimmed, cancellationToken);

        logger.AddedCustomWord(id);

        return new AddCustomWordResult(AddCustomWordOutcome.Success, null);
    }

    private static string? Validate(string word)
    {
        if (word.Length == 0)
        {
            return "Enter a word to add to the custom dictionary.";
        }

        if (word.Length > MaxWordLength)
        {
            return $"A word in the custom dictionary must be no longer than {MaxWordLength} characters.";
        }

        return word.Any(character => char.IsWhiteSpace(character) || char.IsControl(character))
            ? "A word in the custom dictionary must be a single word."
            : null;
    }
}
