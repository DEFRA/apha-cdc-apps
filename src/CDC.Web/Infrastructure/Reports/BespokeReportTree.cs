using CDC.Common.Contracts;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure.Reports;

/// <summary>
/// Lays out the "Create bespoke report" tree: the profile, its sections, and for each question a
/// guidance node followed by a question node, as the legacy page did.
/// </summary>
public static class BespokeReportTree
{
    /// <summary>The name every checkbox posts under.</summary>
    public const string FieldName = "CheckedNodes";

    /// <summary>The prefix of every checkbox id.</summary>
    public const string IdPrefix = "bespoke-tree";

    /// <summary>Marks a guidance node, so "Include/Exclude guidance questions" can find them.</summary>
    public const string GuidanceKind = "guidance";

    /// <summary>Builds the tree with a given set of nodes ticked.</summary>
    /// <param name="content">The profile version's content, which supplies the sections and questions.</param>
    /// <param name="checkedValues">The values of the nodes to tick: a saved template's items, or what was posted.</param>
    /// <returns>The tree.</returns>
    public static TreeViewViewModel Build(ProfileReportContentDto content, IEnumerable<string> checkedValues)
    {
        var selection = BespokeReportNodes.ToSelection(checkedValues);
        HashSet<string> ticked = new(StringComparer.Ordinal);
        List<TreeNodeViewModel> sectionNodes = [];

        foreach (var section in content.Sections)
        {
            List<TreeNodeViewModel> questionNodes = [];

            foreach (var question in section.Questions)
            {
                var name = ProfileTitleHtmlFormatter.ToPlainText(question.Name);
                var guidanceValue = BespokeReportNodes.Value(question.Id, BespokeNodeType.Guidance);
                var questionValue = BespokeReportNodes.Value(question.Id, BespokeNodeType.Question);

                AddIf(selection.Guidance.Contains(question.Id), ticked, guidanceValue);
                AddIf(selection.Questions.Contains(question.Id), ticked, questionValue);
                questionNodes.Add(new TreeNodeViewModel { Value = guidanceValue, Label = $"{question.QuestionNumber} Guidance : {name}", Kind = GuidanceKind });
                questionNodes.Add(new TreeNodeViewModel { Value = questionValue, Label = $"{question.QuestionNumber} : {name}" });
            }

            // The legacy page ticked a node's parent whenever the node itself was ticked.
            var sectionValue = BespokeReportNodes.Value(section.Id, BespokeNodeType.Section);
            AddIf(selection.Sections.Contains(section.Id) || questionNodes.Exists(node => ticked.Contains(node.Value)), ticked, sectionValue);
            sectionNodes.Add(new TreeNodeViewModel
            {
                Value = sectionValue,
                Label = $"{section.SectionNumber} : {ProfileTitleHtmlFormatter.ToPlainText(section.Name)}",
                Children = questionNodes
            });
        }

        var rootValue = BespokeReportNodes.Value(content.ProfileVersionId, BespokeNodeType.Profile);
        AddIf(sectionNodes.Exists(node => ticked.Contains(node.Value)), ticked, rootValue);

        return new TreeViewViewModel
        {
            IdPrefix = IdPrefix,
            FieldName = FieldName,
            Nodes = [new TreeNodeViewModel { Value = rootValue, Label = ProfileTitleHtmlFormatter.ToPlainText(content.FullTitle), Expanded = true, Children = sectionNodes }],
            AllowMultipleSelection = true,
            CascadeSelection = true,
            SelectedValues = ticked
        };
    }

    private static void AddIf(bool condition, HashSet<string> ticked, string value)
    {
        if (condition)
        {
            ticked.Add(value);
        }
    }
}
