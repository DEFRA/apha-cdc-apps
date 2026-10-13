using System.Globalization;
using CDC.Web.Infrastructure;

namespace CDC.Web.Models;

/// <summary>
/// Builds the Compare profile versions tree (profile, then lineage, then version) from the
/// enriched profile search results. Only versions are selectable.
/// </summary>
public static class ProfileVersionTreeBuilder
{
    public const string FieldName = "SelectedVersions";

    private const string CurrentSituationLabel = "Current situation";

    public static ProfileVersionTree Build(IReadOnlyList<ProfileSearchResultDto> profiles, IReadOnlySet<string> selectedValues)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(selectedValues);

        var versions = new Dictionary<Guid, ProfileVersionOption>();
        var nodes = new List<TreeNodeViewModel>();

        foreach (var profile in profiles.OrderBy(item => ProfileTitleHtmlFormatter.ToPlainText(item.Title), StringComparer.OrdinalIgnoreCase))
        {
            var profileTitle = ProfileTitleHtmlFormatter.ToPlainText(profile.Title);
            var lineages = new List<TreeNodeViewModel>();

            AddLineage(lineages, versions, profileTitle, CurrentSituationLabel, $"{profile.Id}-current", profile.PublishedVersions, profile.DraftVersions, selectedValues);

            var scenarioNumber = 0;
            foreach (var scenario in profile.WhatIfScenarios)
            {
                scenarioNumber++;
                var scenarioTitle = ScenarioLabel(scenario, scenarioNumber);
                AddLineage(lineages, versions, profileTitle, scenarioTitle, $"{profile.Id}-{scenario.ScenarioId}", scenario.PublishedVersions, scenario.DraftVersions, selectedValues);
            }

            if (lineages.Count == 0)
            {
                continue;
            }

            nodes.Add(new TreeNodeViewModel
            {
                Value = $"profile-{profile.Id}",
                Label = profileTitle,
                Selectable = false,
                Expanded = lineages.Any(HasSelectedDescendant),
                Children = lineages
            });
        }

        var tree = new TreeViewViewModel
        {
            IdPrefix = "compare-version",
            FieldName = FieldName,
            Nodes = nodes,
            ItemNameSingular = "version",
            ItemNamePlural = "versions",
            AllowMultipleSelection = true,
            SelectedValues = selectedValues
        };

        return new ProfileVersionTree(tree, versions);

        bool HasSelectedDescendant(TreeNodeViewModel node) =>
            node.Children.Any(child => selectedValues.Contains(child.Value));
    }

    private static string ScenarioLabel(ProfileScenarioDto scenario, int number)
    {
        var title = scenario.PublishedVersions.Concat(scenario.DraftVersions)
            .OrderByDescending(version => (version.VersionNumber, version.VersionMinor))
            .Select(version => ProfileTitleHtmlFormatter.ToPlainText(version.Title))
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));

        return string.IsNullOrWhiteSpace(title) ? $"Scenario {number}" : title;
    }

    private static void AddLineage(
        List<TreeNodeViewModel> lineages,
        Dictionary<Guid, ProfileVersionOption> versions,
        string profileTitle,
        string lineageLabel,
        string lineageKey,
        IReadOnlyList<ProfileHistoryItemDto> published,
        IReadOnlyList<ProfileHistoryItemDto> drafts,
        IReadOnlySet<string> selectedValues)
    {
        var rows = published.Select(version => (Version: version, Status: "Published"))
            .Concat(drafts.Select(version => (Version: version, Status: "Draft")))
            .GroupBy(row => row.Version.VersionId)
            .Select(group => group.First())
            .OrderByDescending(row => (row.Version.VersionNumber, row.Version.VersionMinor))
            .ToList();

        if (rows.Count == 0)
        {
            return;
        }

        var children = new List<TreeNodeViewModel>();

        foreach (var (version, status) in rows)
        {
            var text = VersionText(version, status);
            versions[version.VersionId] = new ProfileVersionOption(version.VersionId, $"{profileTitle} - {lineageLabel} - {text}");
            children.Add(new TreeNodeViewModel { Value = version.VersionId.ToString(), Label = text });
        }

        lineages.Add(new TreeNodeViewModel
        {
            Value = $"lineage-{lineageKey}",
            Label = lineageLabel,
            Selectable = false,
            Expanded = true,
            Children = children
        });
    }

    internal static string VersionText(ProfileHistoryItemDto version, string status)
    {
        var from = version.CreatedAtUtc.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        var dates = version.EffectiveToUtc is { } to
            ? $"effective {from} to {to.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}"
            : $"effective from {from}";

        return $"Version {version.VersionNumber}.{version.VersionMinor}, {status}, {dates}";
    }
}
