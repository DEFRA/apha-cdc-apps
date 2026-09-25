namespace CDC.Api.Features.ProfileNotes.Dtos;

/// <summary>A note to delete. Mirrors the legacy <c>ProfileNoteDelete</c> data contract.</summary>
public sealed record ProfileNoteDeleteDto
{
    /// <summary>Gets the note being deleted.</summary>
    public Guid Id { get; init; }

    /// <summary>
    /// Gets the row version read alongside the note, so a concurrent edit can be detected.
    /// </summary>
    public byte[] LastUpdated { get; init; } = [];
}
