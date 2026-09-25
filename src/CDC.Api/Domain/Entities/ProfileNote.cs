using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// A note recorded against a profile version, optionally referencing one or more questions.
/// Mirrors the legacy <c>ProfileNote</c> data contract.
/// </summary>
public sealed record ProfileNote : BaseEntity
{
    /// <summary>Gets the note's rich-text content.</summary>
    public required string NoteText { get; init; }

    /// <summary>Gets the SQL Server <c>rowversion</c> used for optimistic concurrency.</summary>
    public required byte[] LastUpdated { get; init; }

    /// <summary>Gets the questions this note has been raised against.</summary>
    public required IReadOnlyList<QuestionReference> QuestionReferences { get; init; }
}
