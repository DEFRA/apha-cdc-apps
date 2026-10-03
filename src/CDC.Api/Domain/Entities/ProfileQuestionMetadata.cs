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

    /// <summary>Gets the fields that make up the answer, ordered by field number.</summary>
    public required IReadOnlyList<ProfileFieldMetadata> Fields { get; init; }
}
