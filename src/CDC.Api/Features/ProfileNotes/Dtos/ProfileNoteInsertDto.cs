namespace CDC.Api.Features.ProfileNotes.Dtos;

/// <summary>A new note to insert. Mirrors the legacy <c>ProfileNoteInsert</c> data contract.</summary>
public sealed record ProfileNoteInsertDto
{
    /// <summary>Gets the identifier to assign to the new note.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the note's rich-text content.</summary>
    public string NoteText { get; init; } = string.Empty;

    /// <summary>Gets the questions to link the new note to.</summary>
    public IReadOnlyList<QuestionReferenceDto> QuestionReferenceAdds { get; init; } = [];
}
