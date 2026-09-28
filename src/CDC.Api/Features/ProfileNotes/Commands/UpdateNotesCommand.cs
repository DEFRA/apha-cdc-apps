using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileNotes.Dtos;
using MediatR;

namespace CDC.Api.Features.ProfileNotes.Commands;

/// <summary>
/// Inserts, updates and deletes profile notes in a single batch. Mirrors the legacy
/// <c>ProfileNoteChangeset</c> data contract. <c>UserId</c> is deliberately not part of this
/// request - the legacy contract never passed it across the WCF interface either - and is
/// supplied by the service layer instead.
/// </summary>
public sealed record UpdateNotesCommand : IRequest<Result<ProfileNoteChangesetResultDto>>
{
    /// <summary>Gets the profile version the notes belong to.</summary>
    public required Guid ProfileVersionId { get; init; }

    /// <summary>Gets the note type being changed.</summary>
    public required Guid NoteTypeId { get; init; }

    /// <summary>Gets the notes to insert.</summary>
    public IReadOnlyList<ProfileNoteInsertDto> Inserts { get; init; } = [];

    /// <summary>Gets the notes to update.</summary>
    public IReadOnlyList<ProfileNoteUpdateDto> Updates { get; init; } = [];

    /// <summary>Gets the notes to delete.</summary>
    public IReadOnlyList<ProfileNoteDeleteDto> Deletes { get; init; } = [];
}
