namespace CDC.Api.Features.ProfileNotes.Dtos;

/// <summary>A change to an existing note. Mirrors the legacy <c>ProfileNoteUpdate</c> data contract.</summary>
public sealed record ProfileNoteUpdateDto
{
    /// <summary>Gets the note being changed.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the note's new rich-text content.</summary>
    public string NoteText { get; init; } = string.Empty;

    /// <summary>
    /// Gets the row version read alongside the note, so a concurrent edit can be detected.
    /// </summary>
    public byte[] LastUpdated { get; init; } = [];

    /// <summary>Gets the questions to link the note to.</summary>
    public IReadOnlyList<QuestionReferenceDto> QuestionReferenceAdds { get; init; } = [];

    /// <summary>Gets the questions to unlink the note from.</summary>
    public IReadOnlyList<QuestionReferenceDto> QuestionReferenceRemoves { get; init; } = [];
}
