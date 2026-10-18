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

    /// <summary>Gets the rows recorded for each repeating question, ordered by sequence.</summary>
    public IReadOnlyList<ProfileQuestionRow> QuestionRows { get; init; } = [];

    /// <summary>Gets the SQL Server <c>rowversion</c> of the profile version section when the answers were read, which
    /// a save is checked against so it fails when someone else has saved the section since. Empty when the version has
    /// no record of the section.</summary>
    public byte[] LastUpdated { get; init; } = [];
}
