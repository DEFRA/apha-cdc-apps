using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// One row of a repeating profile question for a specific profile version
/// (<c>spgProfileVersionSection</c>'s "Question Rows" result set).
/// </summary>
public sealed record ProfileQuestionRow : BaseEntity
{
    /// <summary>Gets the repeating question the row belongs to.</summary>
    public required Guid QuestionId { get; init; }

    /// <summary>Gets the row's position within the question, lowest first.</summary>
    public required int SequenceNumber { get; init; }
}
