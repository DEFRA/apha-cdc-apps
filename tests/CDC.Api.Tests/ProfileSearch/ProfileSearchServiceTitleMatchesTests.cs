using CDC.Api.Features.ProfileSearch;
using CDC.Api.Features.ProfileSearch.Dtos;
using FluentAssertions;

namespace CDC.Api.Tests.ProfileSearch;

// Parity tests for the legacy ProfileInfoList.TitleMatchesWords behaviour in D2R2-2026-08-17.
public class ProfileSearchServiceTitleMatchesTests
{
    [Theory]
    [InlineData("Avian influenza in poultry", "avian influenza", true)]
    [InlineData("Avian influenza in poultry", "AVIAN INFLUENZA", true)]
    [InlineData("Avian influenza in poultry", "  avian influenza  ", true)]
    [InlineData("Avian metapneumovirus and influenza", "avian influenza", false)]
    [InlineData("Influenza avian", "avian influenza", false)]
    public void ExactWordOrPhrase_MatchesTheWholeSearchTextAsOneSubstring(string title, string searchText, bool expected) =>
        ProfileSearchService.TitleMatches(title, searchText, SearchForType.ExactWordOrPhrase).Should().Be(expected);

    [Theory]
    [InlineData("Avian influenza surveillance in poultry", "avian influenza surveillance", true)]
    [InlineData("Surveillance of influenza in avian species", "avian influenza surveillance", true)]
    [InlineData("Avian  influenza   surveillance", "avian   influenza  surveillance", true)]
    [InlineData("Avian influenza in poultry", "avian influenza surveillance", false)]
    public void AllWords_RequiresEveryWordInAnyOrder(string title, string searchText, bool expected) =>
        ProfileSearchService.TitleMatches(title, searchText, SearchForType.AllWords).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptySearchText_MatchesEverything_InBothModes(string? searchText)
    {
        ProfileSearchService.TitleMatches("Bovine tuberculosis", searchText, SearchForType.ExactWordOrPhrase).Should().BeTrue();
        ProfileSearchService.TitleMatches("Bovine tuberculosis", searchText, SearchForType.AllWords).Should().BeTrue();
    }

    [Fact]
    public void ASingleWord_BehavesIdenticallyInBothModes()
    {
        ProfileSearchService.TitleMatches("Avian influenza", "influenza", SearchForType.ExactWordOrPhrase).Should().BeTrue();
        ProfileSearchService.TitleMatches("Avian influenza", "influenza", SearchForType.AllWords).Should().BeTrue();
    }
}
