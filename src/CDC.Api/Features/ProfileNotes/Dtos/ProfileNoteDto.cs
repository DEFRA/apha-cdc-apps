namespace CDC.Api.Features.ProfileNotes.Dtos;

/// <summary>A note recorded against a profile version.</summary>
public sealed record ProfileNoteDto
{
    /// <summary>Gets the note identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the note's rich-text content.</summary>
    public string NoteText { get; init; } = string.Empty;

    /// <summary>Gets the SQL Server <c>rowversion</c> to send back when updating or deleting.</summary>
    public byte[] LastUpdated { get; init; } = [];

    /// <summary>Gets the questions this note has been raised against.</summary>
    public IReadOnlyList<QuestionReferenceDto> QuestionReferences { get; init; } = [];
}
