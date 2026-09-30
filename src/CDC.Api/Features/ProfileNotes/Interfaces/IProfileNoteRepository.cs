using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileNotes.Commands;

namespace CDC.Api.Features.ProfileNotes.Interfaces;

/// <summary>
/// Data access for profile notes and their question references. Every member maps onto the
/// stored procedures the legacy <c>Profiles.DataAccess.Sql.ProfileNoteService</c> used, so
/// behaviour is preserved.
/// </summary>
public interface IProfileNoteRepository
{
    /// <summary>Reads every profile note type via <c>spgaProfileNoteType</c>.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>Every profile note type.</returns>
    Task<IReadOnlyList<ProfileNoteType>> GetNoteTypesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Reads the notes recorded against one section of a profile version, via
    /// <c>spgProfileVersionNoteBySectionAndType</c>.
    /// </summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="noteTypeId">The note type to filter by.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The matching notes; empty when none have been recorded.</returns>
    Task<IReadOnlyList<ProfileNote>> GetNotesBySectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid noteTypeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Reads every note recorded against a profile version, via <c>spgProfileVersionNoteByType</c>.
    /// </summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="noteTypeId">The note type to filter by.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The matching notes; empty when none have been recorded.</returns>
    Task<IReadOnlyList<ProfileNote>> GetNotesByVersionAsync(
        Guid profileVersionId,
        Guid noteTypeId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies a changeset in a single transaction: for each delete, resolves the affected
    /// sections (<c>spgProfileSectionIdByProfileVersionNoteId</c>) then removes the note
    /// (<c>spdProfileVersionNote</c>); for each insert, creates the note
    /// (<c>spiProfileVersionNote</c>) and its question references
    /// (<c>spiProfileVersionNoteQuestion</c>); for each update, resolves the affected sections,
    /// saves the new text (<c>spuProfileVersionNote</c>) and applies question reference
    /// changes (<c>spiProfileVersionNoteQuestion</c>/<c>spdProfileVersionNoteQuestion</c>);
    /// finally logs the contributing user against every affected section
    /// (<c>spiProfileVersionSectionUser</c>), exactly as the legacy <c>UpdateNotes</c> did.
    /// </summary>
    /// <param name="command">The changeset to apply.</param>
    /// <param name="userId">
    /// The contributing user, logged against each affected section. The legacy contract never
    /// passed this across the WCF interface either; it is resolved by the caller.
    /// </param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The new identifiers and row versions.</returns>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">
    /// Thrown when an update or delete's row version no longer matches the stored value.
    /// </exception>
    Task<ProfileNoteChangesetResult> UpdateNotesAsync(UpdateNotesCommand command, Guid userId, CancellationToken cancellationToken);
}

/// <summary>Outcome of applying a note changeset. See <see cref="Dtos.ProfileNoteChangesetResultDto"/> for the API shape.</summary>
/// <param name="IdInsertList">The identifiers assigned to the newly inserted notes.</param>
/// <param name="LastUpdatedInsertList">The row versions created for the newly inserted notes.</param>
/// <param name="LastUpdatedUpdateList">The row versions created for the updated notes.</param>
public sealed record ProfileNoteChangesetResult(
    IReadOnlyList<Guid> IdInsertList,
    IReadOnlyList<byte[]> LastUpdatedInsertList,
    IReadOnlyList<byte[]> LastUpdatedUpdateList);
