namespace CDC.Api.Infrastructure.Repositories;

/// <summary>
/// Names of the Surveillance Profiles stored procedures backing the profile notes feature.
/// </summary>
public static class ProfileNoteStoredProcedures
{
    /// <summary>Reads every profile note type.</summary>
    public const string GetNoteTypes = "spgaProfileNoteType";

    /// <summary>Reads the notes for one section of a profile version, and their question references, as two result sets.</summary>
    public const string GetNotesBySectionAndType = "spgProfileVersionNoteBySectionAndType";

    /// <summary>Reads every note for a profile version, and their question references, as two result sets.</summary>
    public const string GetNotesByType = "spgProfileVersionNoteByType";

    /// <summary>Inserts a note. Outputs the new row version.</summary>
    public const string InsertProfileVersionNote = "spiProfileVersionNote";

    /// <summary>Links a note to a question.</summary>
    public const string InsertProfileVersionNoteQuestion = "spiProfileVersionNoteQuestion";

    /// <summary>Updates a note's text. Checks <c>@LastUpdated</c> for optimistic concurrency and outputs the new row version.</summary>
    public const string UpdateProfileVersionNote = "spuProfileVersionNote";

    /// <summary>Unlinks a note from a question.</summary>
    public const string DeleteProfileVersionNoteQuestion = "spdProfileVersionNoteQuestion";

    /// <summary>Reads the sections a note is linked to, and its current text.</summary>
    public const string GetProfileSectionIdByNoteId = "spgProfileSectionIdByProfileVersionNoteId";

    /// <summary>Deletes a note. Checks <c>@LastUpdated</c> for optimistic concurrency.</summary>
    public const string DeleteProfileVersionNote = "spdProfileVersionNote";

    /// <summary>Records a user's contribution to a profile version section.</summary>
    public const string InsertProfileVersionSectionUser = "spiProfileVersionSectionUser";
}
