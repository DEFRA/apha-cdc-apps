using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileNotes.Dtos;
using CDC.Api.Features.ProfileNotes.Interfaces;

namespace CDC.Api.Features.ProfileNotes.Mapping;

/// <summary>Projects profile notes domain entities onto the DTOs returned by the API.</summary>
public static class ProfileNoteMappings
{
    /// <summary>Projects a question reference entity.</summary>
    /// <param name="reference">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static QuestionReferenceDto ToDto(this QuestionReference reference) => new()
    {
        ProfileSectionId = reference.ProfileSectionId,
        ProfileQuestionId = reference.ProfileQuestionId
    };

    /// <summary>Projects a profile note entity.</summary>
    /// <param name="note">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ProfileNoteDto ToDto(this ProfileNote note) => new()
    {
        Id = note.Id,
        NoteText = note.NoteText,
        LastUpdated = note.LastUpdated,
        QuestionReferences = [.. note.QuestionReferences.Select(ToDto)]
    };

    /// <summary>Projects a profile note type entity.</summary>
    /// <param name="noteType">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ProfileNoteTypeDto ToDto(this ProfileNoteType noteType) => new()
    {
        Id = noteType.Id,
        Name = noteType.Name,
        PluralName = noteType.PluralName
    };

    /// <summary>Projects a note changeset result.</summary>
    /// <param name="result">The result to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ProfileNoteChangesetResultDto ToDto(this ProfileNoteChangesetResult result) => new()
    {
        IdInsertList = result.IdInsertList,
        LastUpdatedInsertList = result.LastUpdatedInsertList,
        LastUpdatedUpdateList = result.LastUpdatedUpdateList
    };
}
