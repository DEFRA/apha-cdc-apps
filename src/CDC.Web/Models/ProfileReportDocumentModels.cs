using CDC.Common.Contracts;

namespace CDC.Web.Models;

/// <summary>A profile report laid out for printing: a front page, then sections of question tables, then optional references.</summary>
/// <param name="ReportTitle">The report's name, for example "Summary profile".</param>
/// <param name="ProfileTitle">The profile's full title as HTML (it may carry emphasis).</param>
/// <param name="VersionLabel">The version number, for example "1.2".</param>
/// <param name="PublishedDate">The date the version was published, or <see langword="null"/> for a draft.</param>
/// <param name="ShowDraftWatermark">Whether "DRAFT CONTENT" is stamped on every page.</param>
/// <param name="ShowFrontPage">Whether the title page and contents list are printed (a single-section print has none).</param>
/// <param name="Sections">The body sections.</param>
/// <param name="References">The landscape reference tables, or <see langword="null"/> for none.</param>
public sealed record ProfileReportDocumentViewModel(
    string ReportTitle,
    string ProfileTitle,
    string VersionLabel,
    string? PublishedDate,
    bool ShowDraftWatermark,
    bool ShowFrontPage,
    IReadOnlyList<ReportSectionView> Sections,
    IReadOnlyList<ReportReferenceSectionView>? References = null);

/// <summary>One body section of a report.</summary>
/// <param name="Heading">The section heading, or <see langword="null"/> for question tables with no heading above them.</param>
/// <param name="Anchor">The anchor the contents list links to.</param>
/// <param name="Note">The section's standing note, or <see langword="null"/>.</param>
/// <param name="StartsOnNewPage">Whether the section starts on a new page.</param>
/// <param name="Questions">The question tables in the section.</param>
public sealed record ReportSectionView(
    string? Heading,
    string Anchor,
    string? Note,
    bool StartsOnNewPage,
    IReadOnlyList<ReportQuestionView> Questions);

/// <summary>One question table: a heading, optional explanatory line and the answers.</summary>
/// <param name="Heading">The heading text, which includes the question number.</param>
/// <param name="AdditionalInfo">An italic line under the heading, or <see langword="null"/>.</param>
/// <param name="Question">The question and its answers, or <see langword="null"/> for a heading with no answers (the Q&amp;A brief's question 9).</param>
/// <param name="Level">The heading level, 0 for a main heading or 1 for a sub-heading.</param>
/// <param name="GuidanceHtml">The guidance text, which makes this a guidance table (a heading over the text) rather than a question table; otherwise <see langword="null"/>.</param>
public sealed record ReportQuestionView(string Heading, string? AdditionalInfo, ProfileReportQuestionDto? Question, int Level = 1, string? GuidanceHtml = null);

/// <summary>One section's references on the landscape reference pages.</summary>
/// <param name="Heading">The section heading, for example "1. Description".</param>
/// <param name="Tables">One table per kind of reference.</param>
public sealed record ReportReferenceSectionView(string Heading, IReadOnlyList<ReportReferenceTableView> Tables);

/// <summary>A table of one kind of reference for a section.</summary>
/// <param name="Title">The kind of reference, for example "Scientific paper reference".</param>
/// <param name="EmptyText">The line shown when there are none.</param>
/// <param name="Rows">The references, in alphabetical order.</param>
public sealed record ReportReferenceTableView(string Title, string EmptyText, IReadOnlyList<ReportReferenceRow> Rows);

/// <summary>One reference: the questions it is raised against and its text.</summary>
/// <param name="QuestionReferences">The question numbers, for example "2.4, 3.1".</param>
/// <param name="NoteHtml">The reference text; may contain HTML.</param>
public sealed record ReportReferenceRow(string QuestionReferences, string NoteHtml);
