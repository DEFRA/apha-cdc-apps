using CDC.Web.Models;

namespace CDC.Web.Tests.Models;

public class ProfileVersionTreeBuilderTests
{
    private static readonly Guid ProfileId = Guid.Parse("6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f");
    private static readonly Guid ScenarioId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid PublishedV1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid PublishedV2 = Guid.Parse("11111111-1111-1111-1111-222222222222");
    private static readonly Guid DraftV3 = Guid.Parse("11111111-1111-1111-1111-333333333333");
    private static readonly Guid ScenarioV1 = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static ProfileHistoryItemDto Version(Guid id, int number, string title, DateTime from, DateTime? to = null) => new()
    {
        VersionId = id,
        VersionNumber = number,
        VersionMinor = 0,
        Title = title,
        CreatedAtUtc = from,
        EffectiveToUtc = to,
        IsScenario = false
    };

    private static ProfileSearchResultDto Profile(string title = "Anthrax") => new()
    {
        Id = ProfileId,
        Title = title,
        Status = "Published",
        CreatedAtUtc = DateTime.UtcNow,
        ModifiedAtUtc = DateTime.UtcNow,
        IsPublic = false,
        AffectedSpecies = [],
        PublishedVersions =
        [
            Version(PublishedV1, 1, title, new DateTime(2023, 1, 1), new DateTime(2024, 1, 1)),
            Version(PublishedV2, 2, title, new DateTime(2024, 1, 1))
        ],
        DraftVersions = [Version(DraftV3, 3, title, new DateTime(2025, 5, 1))],
        WhatIfScenarios =
        [
            new ProfileScenarioDto
            {
                ScenarioId = ScenarioId,
                PublishedVersions = [Version(ScenarioV1, 1, "<em>Anthrax</em> in wildlife", new DateTime(2024, 3, 1))],
                DraftVersions = []
            }
        ]
    };

    [Fact]
    public void Build_GroupsVersionsUnderProfileAndLineage_NewestFirst()
    {
        var result = ProfileVersionTreeBuilder.Build([Profile()], new HashSet<string>());

        var profile = Assert.Single(result.Tree.Nodes);
        Assert.False(profile.Selectable);
        Assert.Equal("Anthrax", profile.Label);
        Assert.Equal(2, profile.Children.Count);

        var current = profile.Children[0];
        Assert.Equal("Current situation", current.Label);
        Assert.All(current.Children, child => Assert.True(child.Selectable));
        Assert.Equal(
            [DraftV3.ToString(), PublishedV2.ToString(), PublishedV1.ToString()],
            current.Children.Select(child => child.Value));
    }

    [Fact]
    public void Build_LabelsEachVersionWithNumberStatusAndEffectiveDates()
    {
        var result = ProfileVersionTreeBuilder.Build([Profile()], new HashSet<string>());

        var children = result.Tree.Nodes[0].Children[0].Children;
        Assert.Equal("Version 3.0, Draft, effective from 01/05/2025", children[0].Label);
        Assert.Equal("Version 2.0, Published, effective from 01/01/2024", children[1].Label);
        Assert.Equal("Version 1.0, Published, effective 01/01/2023 to 01/01/2024", children[2].Label);
    }

    [Fact]
    public void Build_NamesScenarioFromItsPlainTextTitle()
    {
        var result = ProfileVersionTreeBuilder.Build([Profile()], new HashSet<string>());

        Assert.Equal("Anthrax in wildlife", result.Tree.Nodes[0].Children[1].Label);
    }

    [Fact]
    public void Build_ExposesFullLabelPerVersion()
    {
        var result = ProfileVersionTreeBuilder.Build([Profile()], new HashSet<string>());

        Assert.Equal(4, result.Versions.Count);
        Assert.Equal(
            "Anthrax - Current situation - Version 2.0, Published, effective from 01/01/2024",
            result.Versions[PublishedV2].FullLabel);
    }

    [Fact]
    public void Build_ExpandsOnlyProfilesWithASelectedVersion()
    {
        var selected = new HashSet<string> { PublishedV1.ToString() };

        var result = ProfileVersionTreeBuilder.Build([Profile()], selected);

        Assert.True(result.Tree.Nodes[0].Expanded);
        Assert.Contains(PublishedV1.ToString(), result.Tree.SelectedValues);
        Assert.False(ProfileVersionTreeBuilder.Build([Profile()], new HashSet<string>()).Tree.Nodes[0].Expanded);
    }

    [Fact]
    public void Build_SkipsProfilesWithNoVersions()
    {
        var empty = Profile() with { PublishedVersions = [], DraftVersions = [], WhatIfScenarios = [] };

        var result = ProfileVersionTreeBuilder.Build([empty], new HashSet<string>());

        Assert.Empty(result.Tree.Nodes);
        Assert.Empty(result.Versions);
    }

    [Fact]
    public void Build_FallsBackToNumberedScenarioWhenTitleIsBlank()
    {
        var profile = Profile() with
        {
            WhatIfScenarios =
            [
                new ProfileScenarioDto
                {
                    ScenarioId = ScenarioId,
                    PublishedVersions = [Version(ScenarioV1, 1, " ", new DateTime(2024, 3, 1))],
                    DraftVersions = []
                }
            ]
        };

        var result = ProfileVersionTreeBuilder.Build([profile], new HashSet<string>());

        Assert.Equal("Scenario 1", result.Tree.Nodes[0].Children[1].Label);
    }
}
