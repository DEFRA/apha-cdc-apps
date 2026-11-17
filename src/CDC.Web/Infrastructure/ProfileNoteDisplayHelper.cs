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

    private const int MaximumSlimTextLength = 50;
    private const int SlimTextPrefixLength = 47;

    /// <summary>Removes paragraph tags from a note's text, as the legacy page did to show it on one line in its panel.</summary>
    /// <param name="html">The note's rich-text content.</param>
    /// <returns>The content without <c>&lt;p&gt;</c> and <c>&lt;/p&gt;</c>.</returns>
    public static string StripParagraphTags(string? html) =>
        (html ?? string.Empty).Replace("<p>", string.Empty, StringComparison.Ordinal).Replace("</p>", string.Empty, StringComparison.Ordinal);

    /// <summary>Shortens a note's plain text for a drop-down option or an assistive-technology label: text longer than
    /// 50 characters becomes its first 47 followed by "...", as the legacy <c>NoteTextPlainTextSlim</c> did.</summary>
    /// <param name="html">The note's rich-text content.</param>
    /// <returns>The short plain text.</returns>
    public static string ToSlimText(string? html)
    {
        var plainText = ProfileTitleHtmlFormatter.ToPlainText(html);

        return plainText.Length > MaximumSlimTextLength ? plainText[..SlimTextPrefixLength] + "..." : plainText;
    }

    /// <summary>Lists the questions a note is linked to in one section, in question order (for example "2.1, 2.3"), as the
    /// legacy <c>QuestionReferenceListText</c> did.</summary>
    /// <param name="note">The note.</param>
    /// <param name="section">The section whose questions are listed.</param>
    /// <returns>The question references, or an empty string when the note is linked to none of the section's questions.</returns>
    public static string BuildQuestionReferenceText(ProfileNoteDto note, ProfileSectionMetadataDto section) =>
        string.Join(
            ", ",
            section.Questions
                .Where(question => note.QuestionReferences.Any(reference => reference.ProfileSectionId == section.Id && reference.ProfileQuestionId == question.Id))
                .OrderBy(question => question.QuestionNumber)
                .Select(question => $"{section.SectionNumber}.{question.QuestionNumber}"));

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
