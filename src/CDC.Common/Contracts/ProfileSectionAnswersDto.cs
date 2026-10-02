namespace CDC.Common.Contracts;

/// <summary>
/// The recorded answers for one profile version's reference section. Used directly by both
/// CDC.Api's response and CDC.Web's view model (rather than one concrete type per project)
/// because the two never diverge, so a single shared type is all that is needed to keep the wire
/// shape in sync.
/// </summary>
public sealed record ProfileSectionAnswersDto
{
    /// <summary>Gets the profile version the answers belong to.</summary>
    public Guid ProfileVersionId { get; init; }

    /// <summary>Gets the section the answers belong to.</summary>
    public Guid ProfileSectionId { get; init; }

    /// <summary>Gets the display names of each question in this section.</summary>
    public IReadOnlyList<ProfileQuestionNameDto> QuestionNames { get; init; } = [];

    /// <summary>Gets the recorded field values. Empty when the section is unanswered.</summary>
    public IReadOnlyList<ProfileFieldValueDto> FieldValues { get; init; } = [];
}

/// <summary>The display names of one profile question.</summary>
public sealed record ProfileQuestionNameDto
{
    /// <summary>Gets the question identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the question's full display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the plain-language name shown to non-technical users.</summary>
    public string NonTechnicalName { get; init; } = string.Empty;
}

/// <summary>
/// One stored answer for a profile question field. The common field-value properties live on
/// <see cref="QuestionnaireFieldValueContract"/>; the profile questionnaire additionally supports
/// decimal and date answers.
/// </summary>
public sealed record ProfileFieldValueDto : QuestionnaireFieldValueContract
{
    /// <summary>Gets the answer for a decimal field.</summary>
    public decimal? DecimalValue { get; init; }

    /// <summary>Gets the answer for a date field.</summary>
    public DateTime? DateValue { get; init; }
}
