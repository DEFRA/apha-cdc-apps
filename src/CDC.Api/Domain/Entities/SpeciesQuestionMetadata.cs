using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// A question within a species questionnaire section.
/// </summary>
public sealed record SpeciesQuestionMetadata : BaseEntity
{
    /// <summary>Gets the identifier of the owning section.</summary>
    public required Guid SectionId { get; init; }

    /// <summary>Gets the question text.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the abbreviated question name used in summary views.</summary>
    public required string ShortName { get; init; }

    /// <summary>Gets the position of the question within its section.</summary>
    public required int QuestionNumber { get; init; }

    /// <summary>Gets the fields that make up the answer, ordered by field number.</summary>
    public required IReadOnlyList<SpeciesFieldMetadata> Fields { get; init; }
}
