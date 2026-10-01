namespace CDC.Web.Models;

/// <summary>
/// The recorded answers for one profile version's reference section, as returned by
/// <c>GET /api/profile-sections/answers</c> on CDC.Api. Field names and types mirror the API's
/// <c>ProfileSectionAnswersDto</c> exactly.
/// </summary>
public sealed record ProfileSectionAnswersDto
{
    public Guid ProfileVersionId { get; init; }

    public Guid ProfileSectionId { get; init; }

    /// <summary>Gets the display names of each question in this section.</summary>
    public IReadOnlyList<ProfileQuestionNameDto> QuestionNames { get; init; } = [];

    /// <summary>Gets the recorded field values. Empty when the section is unanswered.</summary>
    public IReadOnlyList<ProfileFieldValueDto> FieldValues { get; init; } = [];
}

/// <summary>The display names of one profile question.</summary>
public sealed record ProfileQuestionNameDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string NonTechnicalName { get; init; } = string.Empty;
}

/// <summary>One stored answer. Exactly one value property is populated, per the field's data type.</summary>
public sealed record ProfileFieldValueDto
{
    public Guid Id { get; init; }

    public Guid QuestionId { get; init; }

    public int FieldNumber { get; init; }

    public bool? BooleanValue { get; init; }

    public Guid? ListValue { get; init; }

    public decimal? DecimalValue { get; init; }

    public DateTime? DateValue { get; init; }

    public string? TextValue { get; init; }
}
