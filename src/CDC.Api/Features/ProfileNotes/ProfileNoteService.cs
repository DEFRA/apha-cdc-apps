using CDC.Api.Features.ProfileNotes.Commands;
using CDC.Api.Features.ProfileNotes.Dtos;
using CDC.Api.Features.ProfileNotes.Interfaces;
using CDC.Api.Features.ProfileNotes.Mapping;
using CDC.Api.Infrastructure;

namespace CDC.Api.Features.ProfileNotes;

/// <summary>
/// Default <see cref="IProfileNoteService"/>: reads and writes through
/// <see cref="IProfileNoteRepository"/> and maps domain entities onto the DTOs the API returns.
/// </summary>
/// <param name="repository">Profile note data access.</param>
/// <param name="auditOptions">The temporary acting user recorded as the contributor until Entra ID supplies the signed-in user.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ProfileNoteService(
    IProfileNoteRepository repository,
    SpeciesAuditOptions auditOptions,
    ILogger<ProfileNoteService> logger)
    : IProfileNoteService
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileNoteTypeDto>> GetNoteTypesAsync(CancellationToken cancellationToken)
    {
        var noteTypes = await repository.GetNoteTypesAsync(cancellationToken);
        logger.RetrievedNoteTypes(noteTypes.Count);

        return [.. noteTypes.Select(noteType => noteType.ToDto())];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileNoteDto>> GetNotesBySectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid noteTypeId,
        CancellationToken cancellationToken)
    {
        var notes = await repository.GetNotesBySectionAsync(profileVersionId, profileSectionId, noteTypeId, cancellationToken);
        logger.RetrievedNotesBySection(notes.Count, profileSectionId, profileVersionId);

        return [.. notes.Select(note => note.ToDto())];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileNoteDto>> GetNotesByVersionAsync(
        Guid profileVersionId,
        Guid noteTypeId,
        CancellationToken cancellationToken)
    {
        var notes = await repository.GetNotesByVersionAsync(profileVersionId, noteTypeId, cancellationToken);
        logger.RetrievedNotesByVersion(notes.Count, profileVersionId);

        return [.. notes.Select(note => note.ToDto())];
    }

    /// <inheritdoc />
    public async Task<ProfileNoteChangesetResultDto> UpdateNotesAsync(UpdateNotesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Each section the change touches records a contributor, which is a foreign key to [User], so a real user id is needed.
        var result = await repository.UpdateNotesAsync(command, auditOptions.AuditUserId, cancellationToken);
        logger.AppliedChangeset(command.ProfileVersionId);

        return result.ToDto();
    }
}
