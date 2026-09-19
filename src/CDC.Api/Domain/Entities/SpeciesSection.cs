namespace CDC.Api.Domain.Entities;

/// <summary>
/// The answered field values for one questionnaire section.
/// </summary>
public sealed record SpeciesSection
{
    /// <summary>Gets the identifier of the section these values belong to.</summary>
    public required Guid SectionId { get; init; }

    /// <summary>Gets the recorded field values. Empty when the section is unanswered.</summary>
    public required IReadOnlyList<SpeciesFieldValue> FieldValues { get; init; }
}
