using CDC.Api.Domain.Entities;
using FluentAssertions;

namespace CDC.Api.Tests.ProfileManagement;

/// <summary>Covers <see cref="ProfileVersionHeader.ToDescription"/>, which mirrors the legacy <c>ProfileVersionInfo.ToString()</c>.</summary>
public class ProfileVersionHeaderDescriptionTests
{
    private static ProfileVersionHeader Header(
        bool isPublished,
        DateTime? effectiveDateTo = null,
        string scenarioTitle = "",
        string profileStatusName = "")
    {
        return new ProfileVersionHeader
        {
            Title = "Anthrax",
            ScenarioTitle = scenarioTitle,
            VersionMajor = 3,
            VersionMinor = 1,
            IsPublished = isPublished,
            EffectiveDateFrom = new DateTime(2020, 1, 2),
            EffectiveDateTo = effectiveDateTo,
            ProfileStatusName = profileStatusName
        };
    }

    [Fact]
    public void ToDescription_ForACurrentPublishedVersion_ShowsOnlyTheStartDate()
    {
        Header(isPublished: true).ToDescription().Should().Be("Anthrax - v3.1 (Published) Valid from 02/01/2020");
    }

    [Fact]
    public void ToDescription_ForASupersededVersion_ShowsTheDateRange()
    {
        Header(isPublished: true, effectiveDateTo: new DateTime(2021, 6, 30)).ToDescription()
            .Should().Be("Anthrax - v3.1 (Published) Valid from 02/01/2020 to 30/06/2021");
    }

    [Fact]
    public void ToDescription_ForACurrentDraftWithAProfileStatus_AppendsTheStatusInBrackets()
    {
        Header(isPublished: false, profileStatusName: "Validation complete").ToDescription()
            .Should().Be("Anthrax - v3.1 (Draft) Valid from 02/01/2020 (Validation complete)");
    }

    [Fact]
    public void ToDescription_ForACurrentDraftWithoutAProfileStatus_OmitsTheBrackets()
    {
        Header(isPublished: false).ToDescription().Should().Be("Anthrax - v3.1 (Draft) Valid from 02/01/2020");
    }

    [Fact]
    public void ToDescription_ForAPublishedVersion_NeverAppendsTheProfileStatus()
    {
        Header(isPublished: true, profileStatusName: "Validation complete").ToDescription()
            .Should().Be("Anthrax - v3.1 (Published) Valid from 02/01/2020");
    }

    [Fact]
    public void ToDescription_ForAScenario_IncludesTheScenarioTitleInTheFullTitle()
    {
        Header(isPublished: true, scenarioTitle: "Wildlife outbreak").ToDescription()
            .Should().Be("Anthrax (Wildlife outbreak) - v3.1 (Published) Valid from 02/01/2020");
    }
}
