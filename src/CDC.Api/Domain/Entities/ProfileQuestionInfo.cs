using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// Summary information about a question, used to populate question pickers. Mirrors the legacy
/// <c>ProfileQuestionInfo</c> data contract.
/// </summary>
public sealed record ProfileQuestionInfo : BaseEntity
{
    /// <summary>Gets the question's full display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the question's position within its section.</summary>
    public required int QuestionNumber { get; init; }
}
