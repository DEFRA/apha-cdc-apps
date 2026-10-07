namespace CDC.Web.Models;

/// <summary>One note/reference row, ready to render in a <see cref="ProfileNoteGroupView"/> table.</summary>
/// <param name="NoteTextHtml">The note's rich-text content, rendered unescaped so authored
/// hyperlinks and formatting display correctly - matches how question "Long Text" answers are
/// already rendered.</param>
/// <param name="QuestionReferenceDisplay">The question number(s) this note is raised against
/// (for example "2.1, 2.3"), or empty when the note has no question references.</param>
public sealed record ProfileNoteRowView(string NoteTextHtml, string QuestionReferenceDisplay);

/// <summary>Data needed to render the shared <c>_ProfileNoteList</c> partial: one note type's
/// entries for the current section (for example "Scientific paper references").</summary>
/// <param name="Heading">The note type's plural display name, used as the section heading.</param>
/// <param name="EmptyMessage">Message shown when there are no notes of this type to display.</param>
/// <param name="Notes">The notes, ordered to match the legacy default sort (alphabetical by
/// plain-text content).</param>
public sealed record ProfileNoteGroupView(string Heading, string EmptyMessage, IReadOnlyList<ProfileNoteRowView> Notes);
