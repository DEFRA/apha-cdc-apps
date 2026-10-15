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
    /// <param name="section">The profile version and section to read, and the question reference lookup.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The notes in alphabetical order, ready to render.</returns>
    public static async Task<ProfileNoteGroupView> LoadGroupAsync(
        IProfileSectionsApiService apiService,
        IReadOnlyList<ProfileNoteTypeDto> noteTypes,
        string normalisedNoteTypeName,
        string fallbackHeading,
        ProfileNoteSectionContext section,
        CancellationToken cancellationToken)
    {
        var noteType = noteTypes.FirstOrDefault(type => NormaliseNoteTypeName(type.Name) == normalisedNoteTypeName);

        if (noteType is null)
        {
            return EmptyGroup(fallbackHeading);
        }

        var notes = await apiService.GetProfileNotesBySectionAsync(section.ProfileVersionId, section.ProfileSectionId, noteType.Id, cancellationToken);

        var heading = string.IsNullOrEmpty(noteType.PluralName) ? fallbackHeading : noteType.PluralName;

        var rows = notes
            .OrderBy(note => ProfileTitleHtmlFormatter.ToPlainText(note.NoteText), StringComparer.OrdinalIgnoreCase)
            .Select(note => new ProfileNoteRowView(
                note.NoteText,
                string.Join(", ", note.QuestionReferences
                    .Select(reference => section.QuestionNumbersById.GetValueOrDefault(reference.ProfileQuestionId))
                    .Where(number => !string.IsNullOrEmpty(number)))))
            .ToList();

        return new ProfileNoteGroupView(heading, BuildEmptyMessage(heading), rows);
    }
}

/// <summary>The section whose notes are being loaded.</summary>
/// <param name="ProfileVersionId">The profile version to read.</param>
/// <param name="ProfileSectionId">The section to read.</param>
/// <param name="QuestionNumbersById">Question id to display reference, for the question reference cell.</param>
public sealed record ProfileNoteSectionContext(
    Guid ProfileVersionId,
    Guid ProfileSectionId,
    IReadOnlyDictionary<Guid, string> QuestionNumbersById);
