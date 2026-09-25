namespace CDC.Api.Features.ProfileNotes.Dtos;

/// <summary>Links a profile note to the question it was raised against.</summary>
public sealed record QuestionReferenceDto
{
    /// <summary>Gets the profile section the question belongs to.</summary>
    public Guid ProfileSectionId { get; init; }

    /// <summary>Gets the question the note is about.</summary>
    public Guid ProfileQuestionId { get; init; }
}
