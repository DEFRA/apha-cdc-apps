namespace CDC.Web.Models;

/// <summary>The summary prioritisation report laid out for printing.</summary>
/// <param name="ReportTitle">The report's name.</param>
/// <param name="ProfileTitle">The profile's full title as HTML.</param>
/// <param name="VersionLabel">The version number, for example "1.2".</param>
/// <param name="PublishedDate">The date the version was published, or <see langword="null"/> for a draft.</param>
/// <param name="ShowDraftWatermark">Whether "DRAFT CONTENT" is stamped on every page.</param>
/// <param name="HasData">Whether prioritisation has been run; when not, only a message is printed.</param>
/// <param name="Blocks">The summary blocks in print order.</param>
/// <param name="CriteriaPages">The detailed criteria scores, one block per pair of profiled species.</param>
public sealed record PrioritisationDocumentViewModel(
    string ReportTitle,
    string ProfileTitle,
    string VersionLabel,
    string? PublishedDate,
    bool ShowDraftWatermark,
    bool HasData,
    IReadOnlyList<PrioritisationBlockView> Blocks,
    IReadOnlyList<PrioritisationCriteriaPageView> CriteriaPages);

/// <summary>One block of the summary part: a question table, a category table or an explanatory note.</summary>
/// <param name="Question">The question table, or <see langword="null"/>.</param>
/// <param name="CategoryTable">The category ranks table, or <see langword="null"/>.</param>
/// <param name="Note">An italic note, or <see langword="null"/>.</param>
public sealed record PrioritisationBlockView(
    ReportQuestionView? Question = null,
    PrioritisationCategoryTableView? CategoryTable = null,
    string? Note = null);

/// <summary>Each profiled species' rank in one prioritisation category.</summary>
/// <param name="Rows">One row per profiled species.</param>
public sealed record PrioritisationCategoryTableView(IReadOnlyList<PrioritisationCategoryRowView> Rows);

/// <summary>One species' rank in a category.</summary>
/// <param name="SpeciesName">The species name; may contain HTML.</param>
/// <param name="Rank">The rank, or the incomplete-data text.</param>
/// <param name="IsPlaceholder">Whether <paramref name="Rank"/> is the grey italic "data incomplete" text.</param>
public sealed record PrioritisationCategoryRowView(string SpeciesName, string Rank, bool IsPlaceholder);

/// <summary>The detailed criteria scores of one or two species.</summary>
/// <param name="Heading">The page heading, for example "Detailed criteria scores".</param>
/// <param name="Anchor">The anchor of the heading.</param>
/// <param name="SpeciesHeading">The species names the page covers, for example "Cattle and Sheep".</param>
/// <param name="SpeciesNames">The column headings, one per species.</param>
/// <param name="Categories">The categories with their criteria.</param>
public sealed record PrioritisationCriteriaPageView(
    string Heading,
    string Anchor,
    string SpeciesHeading,
    IReadOnlyList<string> SpeciesNames,
    IReadOnlyList<PrioritisationCriteriaCategoryView> Categories);

/// <summary>One category's title row (with each species' rank) and its criteria.</summary>
/// <param name="DisplayName">The category's display name; may contain HTML.</param>
/// <param name="BackgroundColour">The title row's background as a CSS colour.</param>
/// <param name="ForegroundColour">The title row's text as a CSS colour.</param>
/// <param name="Ranks">One rank per species.</param>
/// <param name="Criteria">The category's criteria.</param>
public sealed record PrioritisationCriteriaCategoryView(
    string DisplayName,
    string BackgroundColour,
    string ForegroundColour,
    IReadOnlyList<PrioritisationValueView> Ranks,
    IReadOnlyList<PrioritisationCriterionRowView> Criteria);

/// <summary>One criterion and each species' value for it.</summary>
/// <param name="Name">The criterion name; may contain HTML.</param>
/// <param name="Values">One value per species.</param>
public sealed record PrioritisationCriterionRowView(string Name, IReadOnlyList<PrioritisationValueView> Values);

/// <summary>A rank or criterion value.</summary>
/// <param name="Text">The value; may contain HTML unless it is a placeholder.</param>
/// <param name="IsPlaceholder">Whether it is a grey italic "incomplete" or "irrelevant" placeholder.</param>
public sealed record PrioritisationValueView(string Text, bool IsPlaceholder);
