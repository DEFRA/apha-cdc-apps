using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// A profile reference section (for example "Epidemiology"), matching the legacy
/// <c>ProfileSection</c> data contract.
/// </summary>
public sealed record ProfileSectionMetadata : BaseEntity
{
    /// <summary>Gets the section name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the abbreviated section name.</summary>
    public required string ShortName { get; init; }

    /// <summary>Gets the position of the section within the questionnaire.</summary>
    public required int SectionNumber { get; init; }

    /// <summary>Gets the questions in this section, ordered by question number.</summary>
    public required IReadOnlyList<ProfileQuestionMetadata> Questions { get; init; }
}
