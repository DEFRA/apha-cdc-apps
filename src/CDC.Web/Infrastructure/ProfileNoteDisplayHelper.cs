using CDC.Common.Contracts;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Shared helpers for displaying profile notes, used by the References and Further information pages
/// (notes scoped to one section) and <c>NotesModel</c> (notes across the whole profile version),
/// so the note-type matching and question-reference lookup logic is not duplicated between them.
/// </summary>
public static class ProfileNoteDisplayHelper
{
    /// <summary>Normalised name of the "Scientific paper reference" note type.</summary>
    public const string ScientificPaperReferenceType = "SCIENTIFICPAPERREFERENCE";

    /// <summary>Normalised name of the "Legislative reference" note type.</summary>
    public const string LegislativeReferenceType = "LEGISLATIVEREFERENCE";

    /// <summary>Normalised name of the "Source of further information" note type.</summary>
    public const string SourceOfFurtherInformationType = "SOURCEOFFURTHERINFORMATION";

    /// <summary>Strips everything but letters so a reference-data display name (however spaced or
    /// cased) can be matched against the legacy note type's PascalCase identifier, for example
    /// "Scientific paper reference" becomes "SCIENTIFICPAPERREFERENCE".</summary>
    public static string NormaliseNoteTypeName(string name) =>
        new string([.. name.Where(char.IsLetter)]).ToUpperInvariant();

    /// <summary>Builds a lookup from question id to its display reference (for example "2.4"),
    /// used to render the "Question reference" column/cell for a note.</summary>
    public static IReadOnlyDictionary<Guid, string> BuildQuestionNumbersById(IReadOnlyList<ProfileSectionMetadataDto> sections) =>
        sections
            .SelectMany(section => section.Questions.Select(question => (question.Id, Display: $"{section.SectionNumber}.{question.QuestionNumber}")))
            .ToDictionary(pair => pair.Id, pair => pair.Display);

    /// <summary>Builds the legacy-style "There are no X to display." empty-state message for a
    /// note group's heading.</summary>
    public static string BuildEmptyMessage(string heading) => $"There are no {heading.ToLowerInvariant()} to display.";

    /// <summary>Builds an empty note group, shown when a note type is missing or has no notes.</summary>
    /// <param name="heading">The group heading.</param>
    /// <returns>A group with no rows and the legacy empty-state message.</returns>
    public static ProfileNoteGroupView EmptyGroup(string heading) => new(heading, BuildEmptyMessage(heading), []);

    /// <summary>
    /// Loads one section's notes of one type. The note type is resolved by name rather than a hardcoded id,
    /// since note types are reference data; a missing type degrades to an empty group instead of failing the page.
    /// </summary>
    /// <param name="apiService">Typed client for the profile sections endpoints.</param>
    /// <param name="noteTypes">Every note type from CDC.Api.</param>
    /// <param name="normalisedNoteTypeName">The wanted type, for example <see cref="ScientificPaperReferenceType"/>.</param>
    /// <param name="fallbackHeading">The heading used when the type has no plural name.</param>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="questionNumbersById">Question id to display reference, for the question reference cell.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The notes in alphabetical order, ready to render.</returns>
    public static async Task<ProfileNoteGroupView> LoadGroupAsync(
        IProfileSectionsApiService apiService,
        IReadOnlyList<ProfileNoteTypeDto> noteTypes,
        string normalisedNoteTypeName,
        string fallbackHeading,
        Guid profileVersionId,
        Guid profileSectionId,
        IReadOnlyDictionary<Guid, string> questionNumbersById,
        CancellationToken cancellationToken)
    {
        var noteType = noteTypes.FirstOrDefault(type => NormaliseNoteTypeName(type.Name) == normalisedNoteTypeName);

        if (noteType is null)
        {
            return EmptyGroup(fallbackHeading);
        }

        var notes = await apiService.GetProfileNotesBySectionAsync(profileVersionId, profileSectionId, noteType.Id, cancellationToken);

        var heading = string.IsNullOrEmpty(noteType.PluralName) ? fallbackHeading : noteType.PluralName;

        var rows = notes
            .OrderBy(note => ProfileTitleHtmlFormatter.ToPlainText(note.NoteText), StringComparer.OrdinalIgnoreCase)
            .Select(note => new ProfileNoteRowView(
                note.NoteText,
                string.Join(", ", note.QuestionReferences
                    .Select(reference => questionNumbersById.GetValueOrDefault(reference.ProfileQuestionId))
                    .Where(number => !string.IsNullOrEmpty(number)))))
            .ToList();

        return new ProfileNoteGroupView(heading, BuildEmptyMessage(heading), rows);
    }
}
