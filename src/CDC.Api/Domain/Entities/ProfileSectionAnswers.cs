namespace CDC.Api.Domain.Entities;

/// <summary>
/// The recorded answers for one profile version's reference section, as returned by
/// <c>spgProfileVersionSection</c>.
/// </summary>
public sealed record ProfileSectionAnswers
{
    /// <summary>Gets the profile version the answers belong to.</summary>
    public required Guid ProfileVersionId { get; init; }

    /// <summary>Gets the section the answers belong to.</summary>
    public required Guid ProfileSectionId { get; init; }

    /// <summary>Gets the display names of each question in this section.</summary>
    public required IReadOnlyList<ProfileQuestionName> QuestionNames { get; init; }

    /// <summary>Gets the recorded field values. Empty when the section is unanswered.</summary>
    public required IReadOnlyList<ProfileFieldValue> FieldValues { get; init; }
}
