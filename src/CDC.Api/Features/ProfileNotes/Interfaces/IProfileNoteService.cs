using CDC.Api.Features.ProfileNotes.Commands;
using CDC.Api.Features.ProfileNotes.Dtos;

namespace CDC.Api.Features.ProfileNotes.Interfaces;

/// <summary>
/// Application service for the profile notes feature. Owns the mapping between domain entities
/// and the DTOs exposed over HTTP, so MediatR handlers stay thin.
/// </summary>
public interface IProfileNoteService
{
    /// <summary>Gets every profile note type.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Every profile note type.</returns>
    Task<IReadOnlyList<ProfileNoteTypeDto>> GetNoteTypesAsync(CancellationToken cancellationToken);

    /// <summary>Gets the profile notes recorded against one section of a profile version.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="noteTypeId">The note type to filter by.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The matching notes; empty when none have been recorded.</returns>
    Task<IReadOnlyList<ProfileNoteDto>> GetNotesBySectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid noteTypeId,
        CancellationToken cancellationToken);

    /// <summary>Gets every profile note recorded against a profile version.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="noteTypeId">The note type to filter by.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The matching notes; empty when none have been recorded.</returns>
    Task<IReadOnlyList<ProfileNoteDto>> GetNotesByVersionAsync(Guid profileVersionId, Guid noteTypeId, CancellationToken cancellationToken);

    /// <summary>Inserts, updates and deletes profile notes in a single batch.</summary>
    /// <param name="command">The changeset to apply.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new identifiers and row versions.</returns>
    Task<ProfileNoteChangesetResultDto> UpdateNotesAsync(UpdateNotesCommand command, CancellationToken cancellationToken);
}
