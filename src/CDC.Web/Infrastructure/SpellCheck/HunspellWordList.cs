using System.Reflection;
using WeCantSpell.Hunspell;

namespace CDC.Web.Infrastructure.SpellCheck;

/// <summary>
/// The American English Hunspell dictionary (<c>en_US</c>, the language the legacy RadSpell control was set to), embedded in this
/// assembly and read with WeCantSpell.Hunspell, a .NET port of Hunspell. Read once, the first time it is needed; the loaded
/// dictionary is safe to query from many requests at once.
/// </summary>
/// <remarks>
/// The dictionary files and their licence are in <c>Infrastructure/SpellCheck/Dictionaries</c>. To check another language, embed
/// that language's <c>.aff</c> and <c>.dic</c> files under these resource names instead.
/// </remarks>
public sealed class HunspellWordList : ISpellingWordList
{
    internal const string DictionaryResourceName = "CDC.Web.SpellCheck.en_US.dic";
    internal const string AffixResourceName = "CDC.Web.SpellCheck.en_US.aff";

    private readonly Lazy<WordList> wordList = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <inheritdoc />
    public bool Check(string word) => wordList.Value.Check(word);

    /// <inheritdoc />
    public IReadOnlyList<string> Suggest(string word) => [.. wordList.Value.Suggest(word)];

    private static WordList Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var dictionary = Open(assembly, DictionaryResourceName);
        using var affix = Open(assembly, AffixResourceName);

        return WordList.CreateFromStreams(dictionary, affix);
    }

    private static Stream Open(Assembly assembly, string resourceName) =>
        assembly.GetManifestResourceStream(resourceName)
        ?? throw new InvalidOperationException($"The spell check dictionary resource '{resourceName}' is missing from {assembly.GetName().Name}.");
}
