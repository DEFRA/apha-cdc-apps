using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// A section of the species questionnaire.
/// </summary>
public sealed record SpeciesSectionMetadata : BaseEntity
{
    /// <summary>Gets the section name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the abbreviated section name used in summary views.</summary>
    public required string ShortName { get; init; }

    /// <summary>Gets the position of the section within the questionnaire.</summary>
    public required int SectionNumber { get; init; }

    /// <summary>Gets the questions in this section, ordered by question number.</summary>
    public required IReadOnlyList<SpeciesQuestionMetadata> Questions { get; init; }
}
