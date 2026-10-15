namespace CDC.Common.Contracts;

/// <summary>
/// Everything the profile report PDFs print for one profile version: the version's details and
/// each section's questions with their answers resolved for display (selected/unselected list
/// options, formatted numbers and dates, repeating rows and per-species groups, relevance).
/// Returned by <c>GET /api/profile-reports/content/{profileVersionId}</c>.
/// </summary>
public sealed record ProfileReportContentDto
{
    /// <summary>Gets the profile version the content covers.</summary>
    public Guid ProfileVersionId { get; init; }

    /// <summary>Gets the profile the version belongs to.</summary>
    public Guid ProfileId { get; init; }

    /// <summary>Gets the profile's full title with its scenario title in brackets. May contain HTML.</summary>
    public string FullTitle { get; init; } = string.Empty;

    /// <summary>Gets the version number as "major.minor".</summary>
    public string VersionLabel { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether the version is Published rather than Draft.</summary>
    public bool IsPublished { get; init; }

    /// <summary>Gets the date the version became effective.</summary>
    public DateTime EffectiveDateFrom { get; init; }

    /// <summary>
    /// Gets a value indicating whether the "DRAFT CONTENT" watermark is printed: the version is a
    /// draft that is either superseded or not yet at the "validation complete" status.
    /// </summary>
    public bool ShowDraftWatermark { get; init; }

    /// <summary>Gets the profile's sections in section-number order.</summary>
    public IReadOnlyList<ProfileReportSectionDto> Sections { get; init; } = [];
}

/// <summary>One profile section in a report.</summary>
public sealed record ProfileReportSectionDto
{
    /// <summary>Gets the section identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the section's number.</summary>
    public int SectionNumber { get; init; }

    /// <summary>Gets the section's name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the section's abbreviated name, used as the bookmark title.</summary>
    public string ShortName { get; init; } = string.Empty;

    /// <summary>Gets the section's standing note, printed under its heading when it has one.</summary>
    public string Note { get; init; } = string.Empty;

    /// <summary>Gets the section's questions in question-number order.</summary>
    public IReadOnlyList<ProfileReportQuestionDto> Questions { get; init; } = [];
}

/// <summary>How a question's answers are laid out.</summary>
public enum ProfileReportQuestionKind
{
    /// <summary>One set of answers.</summary>
    Standard,

    /// <summary>Any number of answer rows.</summary>
    Repeating,

    /// <summary>One set of answers per profiled species.</summary>
    PerSpecies
}

/// <summary>One question with its answers, ready to print.</summary>
public sealed record ProfileReportQuestionDto
{
    /// <summary>Gets the question identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the question's number within its section.</summary>
    public int QuestionNumber { get; init; }

    /// <summary>Gets the question's full name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the question's plain-language name, used by the Q&amp;A brief.</summary>
    public string NonTechnicalName { get; init; } = string.Empty;

    /// <summary>Gets how the answers are laid out.</summary>
    public ProfileReportQuestionKind Kind { get; init; }

    /// <summary>Gets a value indicating whether the question is relevant, given the answer that controls it.</summary>
    public bool IsRelevant { get; init; } = true;

    /// <summary>Gets the answer groups: one for a standard question, one per row, or one per species.</summary>
    public IReadOnlyList<ProfileReportAnswerGroupDto> Groups { get; init; } = [];
}

/// <summary>One set of field answers: a standard question's answers, a repeating row, or one species.</summary>
public sealed record ProfileReportAnswerGroupDto
{
    /// <summary>Gets the heading printed above the group, or <see langword="null"/> for none.</summary>
    public string? Heading { get; init; }

    /// <summary>Gets a value indicating whether the group is relevant; an irrelevant group prints a "not relevant" line instead of its fields.</summary>
    public bool IsRelevant { get; init; } = true;

    /// <summary>Gets the group's fields in field-number order.</summary>
    public IReadOnlyList<ProfileReportFieldDto> Fields { get; init; } = [];
}

/// <summary>How a field's answer is printed.</summary>
public enum ProfileReportFieldKind
{
    /// <summary>A set of ticked and unticked options (Yes/No, a list, or a multi-value list).</summary>
    Options,

    /// <summary>Text or rich (HTML) text, also decimals and dates already formatted.</summary>
    Text,

    /// <summary>A group of fields repeated per species.</summary>
    FieldGroup
}

/// <summary>One field's answer.</summary>
public sealed record ProfileReportFieldDto
{
    /// <summary>Gets the field label; may contain HTML and may be empty.</summary>
    public string Label { get; init; } = string.Empty;

    /// <summary>Gets how the answer is printed.</summary>
    public ProfileReportFieldKind Kind { get; init; }

    /// <summary>Gets the options for <see cref="ProfileReportFieldKind.Options"/>.</summary>
    public IReadOnlyList<ProfileReportOptionDto> Options { get; init; } = [];

    /// <summary>Gets the text for <see cref="ProfileReportFieldKind.Text"/>; may be HTML. Empty when unanswered.</summary>
    public string Text { get; init; } = string.Empty;

    /// <summary>Gets the per-species groups for <see cref="ProfileReportFieldKind.FieldGroup"/>.</summary>
    public IReadOnlyList<ProfileReportAnswerGroupDto> Groups { get; init; } = [];
}

/// <summary>One option of a Yes/No, list or multi-value list field.</summary>
/// <param name="Text">The option's label; an inactive option carries " (inactive)".</param>
/// <param name="Selected">Whether the option was chosen.</param>
public sealed record ProfileReportOptionDto(string Text, bool Selected);
