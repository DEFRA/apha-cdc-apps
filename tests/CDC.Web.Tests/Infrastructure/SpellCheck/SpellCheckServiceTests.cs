using CDC.Web.Infrastructure.SpellCheck;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Web.Tests.Infrastructure.SpellCheck;

public sealed class SpellCheckServiceTests : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions());
    private readonly FakeWordList wordList = new("cattle", "the", "farmer", "funded", "technical", "non", "don't");
    private readonly FakeCustomWordsApi customWords = new();

    public void Dispose()
    {
        cache.Dispose();
        GC.SuppressFinalize(this);
    }

    private SpellCheckService CreateService() => new(wordList, customWords, cache, NullLogger<SpellCheckService>.Instance);

    [Fact]
    public async Task FindMisspeltWordsAsync_ReportsEachWordNeitherDictionaryKnowsOnce_InTheOrderGiven()
    {
        customWords.Words = ["Defra"];

        var misspelt = await CreateService().FindMisspeltWordsAsync(["cattel", "the", "cattel", "bovine", "Defra"], CancellationToken.None);

        Assert.Equal(["cattel", "bovine"], misspelt);
    }

    [Fact]
    public async Task FindMisspeltWordsAsync_TreatsAWordAsDistinctByItsExactSpelling()
    {
        var misspelt = await CreateService().FindMisspeltWordsAsync(["Cattel", "cattel"], CancellationToken.None);

        Assert.Equal(["Cattel", "cattel"], misspelt);
    }

    [Theory]
    [InlineData("defra")]
    [InlineData("DEFRA")]
    [InlineData("Defra's")]
    [InlineData("Defra\u2019s")]
    public async Task FindMisspeltWordsAsync_AcceptsACustomWord_WhateverItsCase_AndItsPossessive(string word)
    {
        customWords.Words = ["Defra"];

        var misspelt = await CreateService().FindMisspeltWordsAsync([word], CancellationToken.None);

        Assert.Empty(misspelt);
    }

    [Theory]
    [InlineData("non-technical", true)]
    [InlineData("Defra-funded", true)]
    [InlineData("Defra-xyzzy", false)]
    [InlineData("xyzzy-funded", false)]
    public async Task FindMisspeltWordsAsync_AcceptsAHyphenatedWordWhenEveryPartIsKnown_CustomWordsIncluded(string word, bool accepted)
    {
        customWords.Words = ["Defra"];

        var misspelt = await CreateService().FindMisspeltWordsAsync([word], CancellationToken.None);

        Assert.Equal(accepted, misspelt.Count == 0);
    }

    [Fact]
    public async Task FindMisspeltWordsAsync_TurnsATypographicApostropheIntoTheStraightOneBeforeLookingAWordUp()
    {
        var misspelt = await CreateService().FindMisspeltWordsAsync(["don\u2019t"], CancellationToken.None);

        Assert.Empty(misspelt);
        Assert.Equal(["don't"], wordList.Looked);
    }

    [Fact]
    public async Task FindMisspeltWordsAsync_LeavesAloneAWordTooLongToBeAWord_ButChecksOneThatJustFits()
    {
        var longest = new string('a', SpellCheckService.MaxWordLength);
        var tooLong = longest + "a";

        var misspelt = await CreateService().FindMisspeltWordsAsync([longest, tooLong], CancellationToken.None);

        Assert.Equal([longest], misspelt);
    }

    [Fact]
    public async Task FindMisspeltWordsAsync_CarriesOnWithoutTheCustomWords_WhenTheyCannotBeRead_AndTriesAgainNextTime()
    {
        customWords.Words = ["Defra"];
        customWords.FailNextRead = true;
        var service = CreateService();

        var withoutThem = await service.FindMisspeltWordsAsync(["Defra"], CancellationToken.None);
        var withThem = await service.FindMisspeltWordsAsync(["Defra"], CancellationToken.None);

        Assert.Equal(["Defra"], withoutThem);
        Assert.Empty(withThem);
        Assert.Equal(2, customWords.Reads);
    }

    [Fact]
    public async Task FindMisspeltWordsAsync_DoesNotHideACancellation()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        customWords.CancelOnRead = true;

        await Assert.ThrowsAsync<OperationCanceledException>(() => CreateService().FindMisspeltWordsAsync(["cattel"], cancellation.Token));
    }

    [Fact]
    public async Task FindMisspeltWordsAsync_ReadsTheCustomWordsOnce_ForSeveralChecks()
    {
        var service = CreateService();

        await service.FindMisspeltWordsAsync(["cattel"], CancellationToken.None);
        await service.FindMisspeltWordsAsync(["bovine"], CancellationToken.None);
        await service.SuggestAsync("cattel", CancellationToken.None);

        Assert.Equal(1, customWords.Reads);
    }

    [Fact]
    public async Task SuggestAsync_PutsTheCloseCustomWordsFirst_ThenTheDictionarysSuggestions_WithoutRepeats()
    {
        customWords.Words = ["Defra", "APHA"];
        wordList.Suggestions["Defrra"] = ["Debra", "DEFRA", "Defray"];

        var suggestions = await CreateService().SuggestAsync("Defrra", CancellationToken.None);

        Assert.Equal(["Defra", "Debra", "Defray"], suggestions);
    }

    [Fact]
    public async Task SuggestAsync_AllowsOneSlipInAShortCustomWord_AndTwoInALongerOne()
    {
        customWords.Words = ["TB", "bovine"];

        var short1 = await CreateService().SuggestAsync("TV", CancellationToken.None);
        var short2 = await CreateService().SuggestAsync("TVV", CancellationToken.None);
        var long1 = await CreateService().SuggestAsync("bvoine", CancellationToken.None);
        var long2 = await CreateService().SuggestAsync("bvvvne", CancellationToken.None);
        var long3 = await CreateService().SuggestAsync("bvvvnn", CancellationToken.None);

        Assert.Equal(["TB"], short1);
        Assert.Empty(short2);
        Assert.Equal(["bovine"], long1);
        Assert.Equal(["bovine"], long2);
        Assert.Empty(long3);
    }

    [Fact]
    public async Task SuggestAsync_OffersAtMostTenSuggestions()
    {
        wordList.Suggestions["cattel"] = [.. Enumerable.Range(1, 15).Select(number => $"word{number}")];

        var suggestions = await CreateService().SuggestAsync("cattel", CancellationToken.None);

        Assert.Equal(SpellCheckService.MaxSuggestions, suggestions.Count);
        Assert.Equal("word1", suggestions[0]);
    }

    [Fact]
    public async Task SuggestAsync_OffersNothing_ForAnEmptyWord()
    {
        var suggestions = await CreateService().SuggestAsync(string.Empty, CancellationToken.None);

        Assert.Empty(suggestions);
        Assert.Empty(wordList.Suggested);
    }

    [Fact]
    public async Task SuggestAsync_OffersNothing_ForAWordTooLongToBeWorthTheSearch()
    {
        var suggestions = await CreateService().SuggestAsync(new string('a', SpellCheckService.MaxSuggestedWordLength + 1), CancellationToken.None);

        Assert.Empty(suggestions);
        Assert.Empty(wordList.Suggested);
    }

    [Fact]
    public async Task AddCustomWordAsync_MakesTheNextCheckReadTheCustomWordsAgain_WhenTheWordIsAdded()
    {
        var service = CreateService();
        await service.FindMisspeltWordsAsync(["Defra"], CancellationToken.None);
        customWords.AddOutcome = AddCustomWordOutcome.Added;
        customWords.Words = ["Defra"];

        var outcome = await service.AddCustomWordAsync("Defra", CancellationToken.None);
        var misspelt = await service.FindMisspeltWordsAsync(["Defra"], CancellationToken.None);

        Assert.Equal(AddCustomWordOutcome.Added, outcome);
        Assert.Equal(["Defra"], customWords.Added);
        Assert.Empty(misspelt);
        Assert.Equal(2, customWords.Reads);
    }

    [Theory]
    [InlineData(AddCustomWordOutcome.Forbidden)]
    [InlineData(AddCustomWordOutcome.Invalid)]
    [InlineData(AddCustomWordOutcome.Failed)]
    public async Task AddCustomWordAsync_ReturnsWhyTheWordWasRefused_AndKeepsTheCustomWordsItHolds(AddCustomWordOutcome refusal)
    {
        var service = CreateService();
        await service.FindMisspeltWordsAsync(["Defra"], CancellationToken.None);
        customWords.AddOutcome = refusal;

        var outcome = await service.AddCustomWordAsync("Defra", CancellationToken.None);
        await service.FindMisspeltWordsAsync(["Defra"], CancellationToken.None);

        Assert.Equal(refusal, outcome);
        Assert.Equal(1, customWords.Reads);
    }

    [Fact]
    public async Task TheServiceRejectsMissingArguments()
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.FindMisspeltWordsAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.SuggestAsync(null!, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.AddCustomWordAsync(null!, CancellationToken.None));
    }

    private sealed class FakeWordList(params string[] knownWords) : ISpellingWordList
    {
        private readonly HashSet<string> known = new(knownWords, StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string[]> Suggestions { get; } = new(StringComparer.Ordinal);

        public List<string> Looked { get; } = [];

        public List<string> Suggested { get; } = [];

        public bool Check(string word)
        {
            Looked.Add(word);

            return known.Contains(word);
        }

        public IReadOnlyList<string> Suggest(string word)
        {
            Suggested.Add(word);

            return Suggestions.TryGetValue(word, out var suggestions) ? suggestions : [];
        }
    }

    private sealed class FakeCustomWordsApi : ICustomWordsApiService
    {
        public IReadOnlyList<string> Words { get; set; } = [];

        public bool FailNextRead { get; set; }

        public bool CancelOnRead { get; set; }

        public int Reads { get; private set; }

        public AddCustomWordOutcome AddOutcome { get; set; } = AddCustomWordOutcome.Added;

        public List<string> Added { get; } = [];

        public Task<IReadOnlyList<string>> GetCustomWordsAsync(CancellationToken cancellationToken = default)
        {
            Reads++;

            if (CancelOnRead)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            if (FailNextRead)
            {
                FailNextRead = false;
                throw new HttpRequestException("CDC.Api is down");
            }

            return Task.FromResult(Words);
        }

        public Task<AddCustomWordOutcome> AddCustomWordAsync(string word, CancellationToken cancellationToken = default)
        {
            Added.Add(word);

            return Task.FromResult(AddOutcome);
        }
    }
}
