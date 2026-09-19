namespace CDC.Api.Domain.Entities;

/// <summary>
/// The complete species questionnaire structure: sections, their questions, and each
/// question's fields.
/// </summary>
public sealed record SpeciesMetadata
{
    /// <summary>Gets the sections, ordered by section number.</summary>
    public required IReadOnlyList<SpeciesSectionMetadata> Sections { get; init; }
}
