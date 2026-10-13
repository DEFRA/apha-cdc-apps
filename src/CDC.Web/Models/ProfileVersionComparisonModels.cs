namespace CDC.Web.Models;

/// <summary>One selectable profile version in the Compare profile versions tree.</summary>
/// <param name="VersionId">The profile version identifier.</param>
/// <param name="FullLabel">Profile, lineage and version text, for example "Anthrax - Current situation - Version 2.0, Published, effective 01/01/2024 to 01/02/2025".</param>
public sealed record ProfileVersionOption(Guid VersionId, string FullLabel);

/// <summary>The Compare profile versions tree plus a lookup of every selectable version in it.</summary>
public sealed record ProfileVersionTree(
    TreeViewViewModel Tree,
    IReadOnlyDictionary<Guid, ProfileVersionOption> Versions);

/// <summary>The differences between two profile versions, ready to render as a PDF.</summary>
public sealed record ProfileVersionComparisonReport
{
    public required string LeftTitle { get; init; }

    public required string RightTitle { get; init; }

    public required IReadOnlyList<ComparisonSection> Sections { get; init; }
}

/// <summary>One profile section; <see cref="Questions"/> holds only the questions that differ.</summary>
public sealed record ComparisonSection(string Heading, IReadOnlyList<ComparisonQuestion> Questions)
{
    public bool IsIdentical => Questions.Count == 0;
}

/// <summary>A question with at least one field whose answer differs between the two versions.</summary>
public sealed record ComparisonQuestion(string Number, string Text, IReadOnlyList<ComparisonField> Fields);

/// <summary>One differing field. <see cref="IsHtml"/> values are already sanitised.</summary>
public sealed record ComparisonField(string Label, string Left, string Right, bool IsHtml);
