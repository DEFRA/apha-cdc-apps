using CDC.Web.Infrastructure.SpellCheck;

namespace CDC.Web.Tests.Infrastructure.SpellCheck;

// Reads the real embedded en_US dictionary, so these also prove it is in the assembly and parses. It is read once for all of them.
public class HunspellWordListTests
{
    private static readonly HunspellWordList WordList = new();

    [Theory]
    [InlineData("cattle")]
    [InlineData("Bovine")]
    [InlineData("BOVINE")]
    [InlineData("tuberculosis")]
    [InlineData("don't")]
    [InlineData("farmer's")]
    [InlineData("non-technical")]
    [InlineData("x-ray")]
    public void Check_AcceptsAWordSpeltCorrectly(string word) => Assert.True(WordList.Check(word));

    [Theory]
    [InlineData("cattel")]
    [InlineData("bOvine")]
    [InlineData("teh")]
    [InlineData("tubercolosis")]
    [InlineData("farmers'")]
    public void Check_RejectsAWordSpeltWrongly(string word) => Assert.False(WordList.Check(word));

    [Theory]
    [InlineData("cattel", "cattle")]
    [InlineData("tubercolosis", "tuberculosis")]
    [InlineData("teh", "the")]
    public void Suggest_OffersTheCorrection(string misspelt, string expected) =>
        Assert.Contains(expected, WordList.Suggest(misspelt));

    [Fact]
    public void Suggest_OffersNothing_ForAWordWithNothingNearIt() =>
        Assert.Empty(WordList.Suggest("zzzzqqqqxxxxjjjj"));

    [Fact]
    public void TheDictionaryIsEmbeddedInTheAssembly()
    {
        var resources = typeof(HunspellWordList).Assembly.GetManifestResourceNames();

        Assert.Contains(HunspellWordList.DictionaryResourceName, resources);
        Assert.Contains(HunspellWordList.AffixResourceName, resources);
    }
}
