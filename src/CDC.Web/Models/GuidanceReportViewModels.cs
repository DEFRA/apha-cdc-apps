namespace CDC.Web.Models;

/// <summary>The kinds of row a guidance table prints.</summary>
public enum GuidanceRowKind
{
    /// <summary>A question with its number, title and guidance.</summary>
    Question,

    /// <summary>A line of explanation printed in the guidance column.</summary>
    Note,

    /// <summary>A line of explanation printed in red, as the Q&amp;A sheet report does.</summary>
    HighlightedNote
}

/// <summary>One row of a guidance table.</summary>
/// <param name="Kind">What the row holds.</param>
/// <param name="Number">The number printed in the first column, for example <c>2.1</c>.</param>
/// <param name="Name">The question title, which may hold italic markup. Empty for a note.</param>
/// <param name="Guidance">The guidance, which may hold italics, lists and links; or the text of a note.</param>
public sealed record GuidanceReportRowView(GuidanceRowKind Kind, string Number, string Name, string Guidance);

/// <summary>A heading and the table of questions beneath it. The Q&amp;A sheet report has one block with no heading.</summary>
/// <param name="Heading">The heading, or <see langword="null"/> for none.</param>
/// <param name="Rows">The table's rows.</param>
public sealed record GuidanceReportBlockView(string? Heading, IReadOnlyList<GuidanceReportRowView> Rows);

/// <summary>The full, summary and Q&amp;A sheet guidance reports: a title and a table of questions per heading.</summary>
/// <param name="Title">The report's title.</param>
/// <param name="Blocks">The headings and their tables, in print order.</param>
public sealed record GuidanceReportViewModel(string Title, IReadOnlyList<GuidanceReportBlockView> Blocks);

/// <summary>A paragraph or list item of the standard text that opens the SPR guidance report.</summary>
/// <param name="Text">The text.</param>
/// <param name="IsBullet">Whether the text is a list item.</param>
public sealed record SprStandardTextView(string Text, bool IsBullet);

/// <summary>A titled stretch of the standard text that opens the SPR guidance report.</summary>
/// <param name="Title">The title.</param>
/// <param name="Before">The text before the worked example's table, or all of the section's text when it has no table.</param>
/// <param name="HasExample">Whether the worked example's table and total are printed after <paramref name="Before"/>.</param>
/// <param name="After">The text after the worked example.</param>
public sealed record SprStandardSectionView(
    string Title,
    IReadOnlyList<SprStandardTextView> Before,
    bool HasExample,
    IReadOnlyList<SprStandardTextView> After);

/// <summary>A criterion row of the worked example in the SPR guidance report's standard text.</summary>
/// <param name="Cells">The seven cell texts, in column order.</param>
public sealed record SprExampleRowView(IReadOnlyList<string> Cells);

/// <summary>A question's guidance under its summary prioritisation report heading.</summary>
/// <param name="Heading">The heading.</param>
/// <param name="Guidance">The guidance.</param>
public sealed record SprQuestionView(string Heading, string Guidance);

/// <summary>A criterion and the guidance that explains how to score it.</summary>
/// <param name="Name">The criterion name.</param>
/// <param name="Guidance">The guidance, one entry per question it comes from.</param>
public sealed record SprCriterionView(string Name, IReadOnlyList<string> Guidance);

/// <summary>A prioritisation category and its criteria, coloured as on the summary prioritisation report.</summary>
/// <param name="Heading">The category's heading.</param>
/// <param name="Note">A line of explanation under the heading, or <see langword="null"/>.</param>
/// <param name="BackgroundColour">The CSS colour behind the category's heading and criterion names.</param>
/// <param name="ForegroundColour">The CSS colour of the text on that background.</param>
/// <param name="Criteria">The criteria, in print order.</param>
public sealed record SprCategoryView(string Heading, string? Note, string BackgroundColour, string ForegroundColour, IReadOnlyList<SprCriterionView> Criteria);

/// <summary>The summary prioritisation report (SPR) guidance report.</summary>
/// <param name="Title">The report's title.</param>
/// <param name="StandardSections">The standard text that opens the report.</param>
/// <param name="ExampleRows">The rows of the worked example's table.</param>
/// <param name="ExampleTotalText">The line stating the example's overall maximum score.</param>
/// <param name="ExampleTotalValue">The overall maximum score.</param>
/// <param name="ExampleTotalNote">The note under the example's total.</param>
/// <param name="Questions">The ten questions printed under the report's headings.</param>
/// <param name="CriteriaTitle">The title above the detailed criteria.</param>
/// <param name="Categories">The categories of detailed criteria.</param>
public sealed record SprGuidanceReportViewModel(
    string Title,
    IReadOnlyList<SprStandardSectionView> StandardSections,
    IReadOnlyList<SprExampleRowView> ExampleRows,
    string ExampleTotalText,
    string ExampleTotalValue,
    string ExampleTotalNote,
    IReadOnlyList<SprQuestionView> Questions,
    string CriteriaTitle,
    IReadOnlyList<SprCategoryView> Categories);
