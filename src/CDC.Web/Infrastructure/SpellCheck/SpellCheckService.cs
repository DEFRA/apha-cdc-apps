using Microsoft.Extensions.Caching.Memory;

namespace CDC.Web.Infrastructure.SpellCheck;

/// <summary>
/// Default <see cref="ISpellCheckService"/>: the English dictionary, with the custom dictionary kept in CDC.Api added to it. The
/// custom words are held for a minute, so a spell check costs CDC.Api one call at most, and a word added is accepted by every
/// instance of this app within that minute.
/// </summary>
/// <param name="dictionary">The English dictionary.</param>
/// <param name="customWordsApi">The custom dictionary in CDC.Api.</param>
/// <param name="cache">Holds the custom words between spell checks.</param>
/// <param name="logger">Structured logger.</param>
public sealed class SpellCheckService(
    ISpellingWordList dictionary,
    ICustomWordsApiService customWordsApi,
    IMemoryCache cache,
    ILogger<SpellCheckService> logger) : ISpellCheckService
{
    /// <summary>The longest word that is looked up. A longer one is not a word - an address, say - and is left alone.</summary>
    internal const int MaxWordLength = 100;

    /// <summary>The longest word that is given suggestions, which are costly to find: they take about as long as the word is long, squared.</summary>
    internal const int MaxSuggestedWordLength = 64;

    /// <summary>The most suggestions offered for a word.</summary>
    internal const int MaxSuggestions = 10;

    private const string CustomWordsCacheKey = "SpellCheck.CustomWords";
    private const char Hyphen = '-';
    private const char StraightApostrophe = '\'';
    private const char CurlyApostrophe = '\u2019';

    private static readonly TimeSpan CustomWordsLifetime = TimeSpan.FromMinutes(1);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> FindMisspeltWordsAsync(IEnumerable<string> words, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(words);

        var customWords = await GetCustomWordsAsync(cancellationToken);
        var misspelt = words
            .Distinct(StringComparer.Ordinal)
            .Where(word => word.Length <= MaxWordLength && !IsCorrect(Normalise(word), customWords))
            .ToList();

        logger.CheckedWords(misspelt.Count);

        return misspelt;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> SuggestAsync(string word, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(word);

        if (word.Length == 0 || word.Length > MaxSuggestedWordLength)
        {
            return [];
        }

        var normalised = Normalise(word);
        var customWords = await GetCustomWordsAsync(cancellationToken);

        return
        [
            .. customWords
                .Where(customWord => IsClose(normalised, customWord))
                .Order(StringComparer.OrdinalIgnoreCase)
                .Concat(dictionary.Suggest(normalised))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxSuggestions)
        ];
    }

    /// <inheritdoc />
    public async Task<AddCustomWordOutcome> AddCustomWordAsync(string word, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(word);

        var outcome = await customWordsApi.AddCustomWordAsync(word, cancellationToken);

        if (outcome == AddCustomWordOutcome.Added)
        {
            // The next spell check reads the dictionary afresh, with the new word in it.
            cache.Remove(CustomWordsCacheKey);
            logger.AddedCustomWord();
        }
        else
        {
            logger.AddCustomWordRefused(outcome);
        }

        return outcome;
    }

    private bool IsCorrect(string word, HashSet<string> customWords)
    {
        if (customWords.Contains(word) || dictionary.Check(word))
        {
            return true;
        }

        // The dictionary accepts a possessive of any noun in it, but not of a word added to the custom dictionary.
        if (word.EndsWith("'s", StringComparison.OrdinalIgnoreCase) && customWords.Contains(word[..^2]))
        {
            return true;
        }

        // Like the dictionary's own rule for a hyphenated word - each part must be right - but counting the custom words too.
        return word.Contains(Hyphen, StringComparison.Ordinal)
            && word.Split(Hyphen, StringSplitOptions.RemoveEmptyEntries).All(part => IsCorrect(part, customWords));
    }

    private async Task<HashSet<string>> GetCustomWordsAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGetValue(CustomWordsCacheKey, out HashSet<string>? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var words = new HashSet<string>((await customWordsApi.GetCustomWordsAsync(cancellationToken)).Select(Normalise), StringComparer.OrdinalIgnoreCase);
            cache.Set(CustomWordsCacheKey, words, CustomWordsLifetime);

            return words;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // As in legacy, the spell check carries on without the custom words rather than not running. Nothing is cached, so the
            // next one tries again.
            logger.CustomWordsUnavailable(exception);

            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    // The dictionary's apostrophe is the straight one, so a typographic one in the text is turned into it.
    private static string Normalise(string word) => word.Replace(CurlyApostrophe, StraightApostrophe);

    // A custom word is suggested for a word that is a slip or two away from it: one for a short word, two for a longer one.
    private static bool IsClose(string word, string customWord)
    {
        var allowed = Math.Max(word.Length, customWord.Length) <= 4 ? 1 : 2;

        return Math.Abs(word.Length - customWord.Length) <= allowed
            && EditDistance(word.ToUpperInvariant(), customWord.ToUpperInvariant()) <= allowed;
    }

    // The fewest insertions, deletions, substitutions and swaps of neighbouring letters that turn one word into the other.
    private static int EditDistance(string source, string target)
    {
        var columns = target.Length + 1;
        var distances = new int[(source.Length + 1) * columns];

        for (var row = 0; row <= source.Length; row++)
        {
            distances[row * columns] = row;
        }

        for (var column = 0; column <= target.Length; column++)
        {
            distances[column] = column;
        }

        for (var row = 1; row <= source.Length; row++)
        {
            for (var column = 1; column <= target.Length; column++)
            {
                var substitution = source[row - 1] == target[column - 1] ? 0 : 1;
                var best = Math.Min(
                    Math.Min(distances[((row - 1) * columns) + column] + 1, distances[(row * columns) + column - 1] + 1),
                    distances[((row - 1) * columns) + column - 1] + substitution);

                if (row > 1 && column > 1 && source[row - 1] == target[column - 2] && source[row - 2] == target[column - 1])
                {
                    best = Math.Min(best, distances[((row - 2) * columns) + column - 2] + 1);
                }

                distances[(row * columns) + column] = best;
            }
        }

        return distances[(source.Length * columns) + target.Length];
    }
}
