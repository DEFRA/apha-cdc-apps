namespace CDC.Web.Models;

/// <summary>Which panel is open beneath the sources table on the Further information tab.</summary>
public enum ProfileNotePanel
{
    /// <summary>No panel is open.</summary>
    None,

    /// <summary>The "Add a source" panel, for entering a new note.</summary>
    Add,

    /// <summary>The "Select a source" panel, for picking an existing note from the master list.</summary>
    Select,

    /// <summary>The "View a source" panel, for changing which questions an existing note is linked to.</summary>
    View
}

/// <summary>One row of the sources table.</summary>
/// <param name="Id">The note.</param>
/// <param name="TextHtml">The note's rich-text content, rendered unescaped so authored links and formatting display correctly.</param>
/// <param name="PlainText">The note's content with tags stripped, used to sort the first column.</param>
/// <param name="QuestionReferenceDisplay">The questions the note is linked to in this section (for example "2.1, 2.3").</param>
/// <param name="SlimText">A short plain-text form of the note, to tell the row's actions apart for assistive technology.</param>
/// <param name="LastUpdated">The note's row version, Base64 encoded, posted back by the delete confirmation.</param>
public sealed record ProfileNoteGridRow(
    Guid Id,
    string TextHtml,
    string PlainText,
    string QuestionReferenceDisplay,
    string SlimText,
    string LastUpdated);

/// <summary>One entry of the master-list drop-down.</summary>
/// <param name="Id">The note.</param>
/// <param name="Text">The note's short plain text.</param>
/// <param name="IsSelected">Whether this is the note chosen.</param>
public sealed record ProfileNoteChoice(Guid Id, string Text, bool IsSelected);

/// <summary>One question checkbox in the panel.</summary>
/// <param name="Id">The question.</param>
/// <param name="Number">The question's reference (for example "2.4").</param>
/// <param name="Name">The question's name, which may contain basic formatting.</param>
/// <param name="IsTicked">Whether the note is linked to this question.</param>
public sealed record QuestionReferenceChoice(Guid Id, string Number, string Name, bool IsTicked);

/// <summary>A message in the panel's error summary, linked to the field it is about.</summary>
/// <param name="FieldId">The id of the field the message links to.</param>
/// <param name="Message">What is wrong and how to fix it.</param>
public sealed record ProfileNoteFormError(string FieldId, string Message);

/// <summary>The fields the Add, Select and View panels post.</summary>
public sealed class ProfileNoteInput
{
    /// <summary>Gets or sets which panel was posted: <c>add</c>, <c>select</c> or <c>view</c>.</summary>
    public string? Mode { get; set; }

    /// <summary>Gets or sets the note chosen in the Select panel, or shown in the View panel.</summary>
    public Guid? NoteId { get; set; }

    /// <summary>Gets or sets the text of a new note, from the Add panel.</summary>
    public string NoteText { get; set; } = string.Empty;

    /// <summary>Gets or sets the Base64 row version of the note being linked, read when its panel opened.</summary>
    public string LastUpdated { get; set; } = string.Empty;

    /// <summary>Gets or sets the questions ticked.</summary>
    public List<Guid> QuestionIds { get; set; } = [];
}
