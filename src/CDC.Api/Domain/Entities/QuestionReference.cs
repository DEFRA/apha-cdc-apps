namespace CDC.Api.Domain.Entities;

/// <summary>
/// Links a profile note to the question it was raised against. Mirrors the legacy
/// <c>QuestionReference</c> data contract. Has no identifier of its own: the pair of
/// <see cref="ProfileSectionId"/> and <see cref="ProfileQuestionId"/> is the key.
/// </summary>
public sealed record QuestionReference
{
    /// <summary>Gets the profile section the question belongs to.</summary>
    public required Guid ProfileSectionId { get; init; }

    /// <summary>Gets the question the note is about.</summary>
    public required Guid ProfileQuestionId { get; init; }
}
