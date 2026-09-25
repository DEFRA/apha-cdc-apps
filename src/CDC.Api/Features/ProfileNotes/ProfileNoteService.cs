using CDC.Api.Features.ProfileNotes.Commands;
using CDC.Api.Features.ProfileNotes.Dtos;
using CDC.Api.Features.ProfileNotes.Interfaces;
using CDC.Api.Features.ProfileNotes.Mapping;

namespace CDC.Api.Features.ProfileNotes;

/// <summary>
/// Default <see cref="IProfileNoteService"/>: reads and writes through
/// <see cref="IProfileNoteRepository"/> and maps domain entities onto the DTOs the API returns.
/// </summary>
/// <param name="repository">Profile note data access.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ProfileNoteService(IProfileNoteRepository repository, ILogger<ProfileNoteService> logger)
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

        // No authenticated user is available yet; the legacy contract never passed UserId
        // across the WCF interface either, so this mirrors the same placeholder CDC.Api
        // already uses elsewhere (ProfileRepository.GetAllProfilesAsync) until Entra ID lands.
        var result = await repository.UpdateNotesAsync(command, Guid.Empty, cancellationToken);
        logger.AppliedChangeset(command.ProfileVersionId);

        return result.ToDto();
    }
}
