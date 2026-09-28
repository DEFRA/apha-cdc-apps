namespace CDC.Api.Features.ProfileNotes.Dtos;

/// <summary>A category of profile note (for example, "Comment" or "Review Point").</summary>
public sealed record ProfileNoteTypeDto
{
    /// <summary>Gets the note type identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the singular display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the plural display name.</summary>
    public string PluralName { get; init; } = string.Empty;
}
