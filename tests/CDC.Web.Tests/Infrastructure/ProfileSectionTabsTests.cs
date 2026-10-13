using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class ProfileSectionTabsTests
{
    [Theory]
    [InlineData(ProfileSectionTab.Questions, "/SurveillanceProfiles/EditProfileQuestions")]
    [InlineData(ProfileSectionTab.References, "/SurveillanceProfiles/EditProfileReferences")]
    [InlineData(ProfileSectionTab.FurtherInformation, "/SurveillanceProfiles/EditProfileFurtherInformation")]
    [InlineData(ProfileSectionTab.ReviewComments, "/SurveillanceProfiles/EditProfileReviewComments")]
    [InlineData(ProfileSectionTab.AllRevisionDates, "/SurveillanceProfiles/EditProfileAllRevisionDates")]
    [InlineData(ProfileSectionTab.Contributions, "/SurveillanceProfiles/EditProfileContributions")]
    public void PagePath_MapsEachTabToItsOwnPage(ProfileSectionTab tab, string expected) =>
        Assert.Equal(expected, ProfileSectionTabs.PagePath(tab));

    [Fact]
    public void PagePath_Throws_ForAnUnknownTab() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ProfileSectionTabs.PagePath((ProfileSectionTab)99));

    [Theory]
    [InlineData(ProfileSectionTab.References, "References")]
    [InlineData(ProfileSectionTab.FurtherInformation, "Further information")]
    [InlineData(ProfileSectionTab.ReviewComments, "Review comments")]
    [InlineData(ProfileSectionTab.AllRevisionDates, "All Revision dates")]
    [InlineData(ProfileSectionTab.Contributions, "Contributions")]
    public void Text_UsesTheLegacyTabText(ProfileSectionTab tab, string expected) =>
        Assert.Equal(expected, ProfileSectionTabs.Text(tab));

    [Fact]
    public void Text_Throws_ForTheQuestionsTab_BecauseItIsNamedAfterTheSection() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => ProfileSectionTabs.Text(ProfileSectionTab.Questions));

    [Fact]
    public void VisibleTabs_ListsOnlyTheAlwaysVisibleTabs_WhenNothingIsPermitted() =>
        Assert.Equal(
            [ProfileSectionTab.Questions, ProfileSectionTab.References, ProfileSectionTab.FurtherInformation],
            ProfileSectionTabs.VisibleTabs(false, false, false));

    [Fact]
    public void VisibleTabs_PutsRevisionDatesBeforeContributions_AsInTheLegacyTabStrip() =>
        Assert.Equal(
            [
                ProfileSectionTab.Questions,
                ProfileSectionTab.References,
                ProfileSectionTab.FurtherInformation,
                ProfileSectionTab.ReviewComments,
                ProfileSectionTab.AllRevisionDates,
                ProfileSectionTab.Contributions
            ],
            ProfileSectionTabs.VisibleTabs(true, true, true));

    [Theory]
    [InlineData(true, false, false, ProfileSectionTab.ReviewComments)]
    [InlineData(false, true, false, ProfileSectionTab.AllRevisionDates)]
    [InlineData(false, false, true, ProfileSectionTab.Contributions)]
    public void VisibleTabs_AddsEachPermittedTabOnItsOwn(bool comments, bool revisionDates, bool contributions, ProfileSectionTab expected) =>
        Assert.Equal(
            [ProfileSectionTab.Questions, ProfileSectionTab.References, ProfileSectionTab.FurtherInformation, expected],
            ProfileSectionTabs.VisibleTabs(comments, revisionDates, contributions));

    [Theory]
    [InlineData("SUMMARY", "Summary")]
    [InlineData("GEOGRAPHIC DISTRIBUTION AND LEVELS OF DISEASE", "Geographic distribution & levels of disease")]
    [InlineData("Epi", "Epi")]
    [InlineData("A", "A")]
    [InlineData("", "")]
    public void FormatShortName_CapitalisesOnlyTheFirstLetter_AndShowsAndAsAnAmpersand(string shortName, string expected) =>
        Assert.Equal(expected, ProfileSectionTabs.FormatShortName(shortName));
}
