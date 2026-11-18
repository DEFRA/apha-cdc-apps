using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// A question within a profile reference section, matching the legacy
/// <c>ProfileQuestion</c> data contract.
/// </summary>
public sealed record ProfileQuestionMetadata : BaseEntity
{
    /// <summary>Gets the identifier of the owning section.</summary>
    public required Guid SectionId { get; init; }

    /// <summary>Gets the abbreviated question name.</summary>
    public required string ShortName { get; init; }

    /// <summary>Gets the position of the question within its section.</summary>
    public required int QuestionNumber { get; init; }

    /// <summary>Gets a value indicating whether the question is answered once per affected species.</summary>
    public required bool IsPerSpecies { get; init; }

    /// <summary>Gets a value indicating whether the question allows repeating rows of answers.</summary>
    public required bool IsRepeating { get; init; }

    /// <summary>Gets the field whose answer decides whether the question is relevant, or <see cref="Guid.Empty"/> when it is always relevant.</summary>
    public Guid IrrelevanceFieldId { get; init; }

    /// <summary>Gets the controlling field's list value the irrelevance test compares against, or <see cref="Guid.Empty"/> for a Yes/No field.</summary>
    public Guid IrrelevanceValueId { get; init; }

    /// <summary>Gets the flag the relevance test applies: with <see cref="IrrelevanceValueId"/>, the question is relevant when the
    /// answer equals that value (flag set) or differs from it (flag clear); for a Yes/No field, relevant when the answer differs from the flag.</summary>
    public bool IrrelevanceFlag { get; init; }

    /// <summary>Gets the fields that make up the answer, ordered by field number.</summary>
    public required IReadOnlyList<ProfileFieldMetadata> Fields { get; init; }
}
