namespace CDC.Api.Features.ProfileSections.Dtos;

/// <summary>
/// The recorded answers for one profile version's reference section.
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

/// <summary>One stored answer. Exactly one value property is populated, per the field's data type.</summary>
public sealed record ProfileFieldValueDto
{
    /// <summary>Gets the field value identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the identifier of the question the field belongs to.</summary>
    public Guid QuestionId { get; init; }

    /// <summary>Gets the position of the field within its question.</summary>
    public int FieldNumber { get; init; }

    /// <summary>Gets the answer for a boolean field.</summary>
    public bool? BooleanValue { get; init; }

    /// <summary>Gets the selected reference data item for a list field.</summary>
    public Guid? ListValue { get; init; }

    /// <summary>Gets the answer for a decimal field.</summary>
    public decimal? DecimalValue { get; init; }

    /// <summary>Gets the answer for a date field.</summary>
    public DateTime? DateValue { get; init; }

    /// <summary>Gets the answer for a text or long-text (rich/HTML) field.</summary>
    public string? TextValue { get; init; }
}
