namespace CDC.Common.Contracts;

/// <summary>
/// Everything the four user guidance reports print: every profile section with each question's titles and
/// guidance, and the questions behind each prioritisation criterion. Used directly by both CDC.Api's response
/// and CDC.Web's report builders, because the two never diverge.
/// </summary>
public sealed record ProfileGuidanceContentDto
{
    /// <summary>Gets the profile sections, in section-number order.</summary>
    public IReadOnlyList<ProfileGuidanceSectionDto> Sections { get; init; } = [];

    /// <summary>Gets the prioritisation categories shown on the summary prioritisation report, in display order.</summary>
    public IReadOnlyList<PrioritisationGuidanceCategoryDto> PrioritisationCategories { get; init; } = [];
}

/// <summary>A profile section and its questions.</summary>
public sealed record ProfileGuidanceSectionDto
{
    /// <summary>Gets the section identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the position of the section within the questionnaire.</summary>
    public int SectionNumber { get; init; }

    /// <summary>Gets the section name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the questions, in question-number order.</summary>
    public IReadOnlyList<ProfileGuidanceQuestionDto> Questions { get; init; } = [];
}

/// <summary>A question with its titles and guidance.</summary>
public sealed record ProfileGuidanceQuestionDto
{
    /// <summary>Gets the question identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the position of the question within its section.</summary>
    public int QuestionNumber { get; init; }

    /// <summary>Gets the question's full display name, which may hold italic markup.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the plain-language name shown to non-technical users, which may hold italic markup.</summary>
    public string NonTechnicalName { get; init; } = string.Empty;

    /// <summary>Gets the guidance text, which may hold italics, lists and links.</summary>
    public string UserGuidance { get; init; } = string.Empty;
}

/// <summary>A prioritisation category and the criteria scored within it.</summary>
public sealed record PrioritisationGuidanceCategoryDto
{
    /// <summary>Gets the category identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the heading the report prints for the category.</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>Gets the criteria, each listed once, in criterion-code order.</summary>
    public IReadOnlyList<PrioritisationGuidanceCriterionDto> Criteria { get; init; } = [];
}

/// <summary>A prioritisation criterion and the question whose guidance explains how to score it.</summary>
public sealed record PrioritisationGuidanceCriterionDto
{
    /// <summary>Gets the question whose guidance applies to the criterion.</summary>
    public Guid ProfileQuestionId { get; init; }

    /// <summary>Gets the criterion code, for example <c>PMR01</c>.</summary>
    public string CriterionCode { get; init; } = string.Empty;

    /// <summary>Gets the criterion name.</summary>
    public string CriterionName { get; init; } = string.Empty;
}
