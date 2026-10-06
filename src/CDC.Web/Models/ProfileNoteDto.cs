namespace CDC.Web.Models;

/// <summary>A category of profile note (for example "Scientific paper reference").</summary>
public sealed record ProfileNoteTypeDto
{
    /// <summary>Gets the note type identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the singular display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the plural display name.</summary>
    public string PluralName { get; init; } = string.Empty;
}

/// <summary>A note recorded against a profile version (a reference or further information entry).</summary>
public sealed record ProfileNoteDto
{
    /// <summary>Gets the note identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the note's rich-text content.</summary>
    public string NoteText { get; init; } = string.Empty;

    /// <summary>Gets the questions this note has been raised against.</summary>
    public IReadOnlyList<QuestionReferenceDto> QuestionReferences { get; init; } = [];
}

/// <summary>Links a profile note to the question it was raised against.</summary>
public sealed record QuestionReferenceDto
{
    /// <summary>Gets the profile section the question belongs to.</summary>
    public Guid ProfileSectionId { get; init; }

    /// <summary>Gets the question the note is about.</summary>
    public Guid ProfileQuestionId { get; init; }
}
