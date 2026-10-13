using CDC.Common.Contracts;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Shared helpers for displaying profile notes, used by both <c>EditProfileQuestionsModel</c>
/// (notes scoped to one section) and <c>NotesModel</c> (notes across the whole profile version),
/// so the note-type matching and question-reference lookup logic is not duplicated between them.
/// </summary>
public static class ProfileNoteDisplayHelper
{
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
}
