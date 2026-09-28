using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// A category of profile note (for example, "Comment" or "Review Point"). Mirrors the legacy
/// <c>ProfileNoteType</c> data contract.
/// </summary>
public sealed record ProfileNoteType : BaseEntity
{
    /// <summary>Gets the singular display name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the plural display name.</summary>
    public required string PluralName { get; init; }
}
