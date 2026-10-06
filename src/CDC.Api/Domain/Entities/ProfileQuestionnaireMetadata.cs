namespace CDC.Api.Domain.Entities;

/// <summary>
/// The complete profile questionnaire structure: the 16 fixed profile reference sections,
/// their questions, and each question's fields. The structure is the same for every profile;
/// recorded answers are retrieved separately per profile version.
/// </summary>
public sealed record ProfileQuestionnaireMetadata
{
    /// <summary>Gets the sections, ordered by section number.</summary>
    public required IReadOnlyList<ProfileSectionMetadata> Sections { get; init; }
}
