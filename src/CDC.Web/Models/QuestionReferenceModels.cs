namespace CDC.Web.Models;

/// <summary>The fields the "Add/Select a reference to question" dialog of the Questions tab posts.</summary>
public sealed class QuestionReferenceInput
{
    /// <summary>Gets or sets the question the reference is for.</summary>
    public Guid QuestionId { get; set; }

    /// <summary>Gets or sets what the user chose to do: <c>add</c> a new reference or <c>select</c> an existing one.</summary>
    public string? Mode { get; set; }

    /// <summary>Gets or sets the type of reference.</summary>
    public Guid? NoteTypeId { get; set; }

    /// <summary>Gets or sets the text of a new reference.</summary>
    public string NoteText { get; set; } = string.Empty;

    /// <summary>Gets or sets the existing reference that was selected.</summary>
    public Guid? NoteId { get; set; }

    /// <summary>Gets or sets the Base64 row version of the selected reference, read when it was offered.</summary>
    public string LastUpdated { get; set; } = string.Empty;

    /// <summary>Gets or sets the other questions the reference is applied to as well.</summary>
    public List<Guid> QuestionIds { get; set; } = [];
}

/// <summary>One existing reference the dialog offers to select.</summary>
/// <param name="Id">The reference.</param>
/// <param name="Text">The reference's short plain text, for the drop-down.</param>
/// <param name="Html">The reference's full text as HTML that is safe to show.</param>
/// <param name="LastUpdated">The reference's row version, Base64 encoded, posted back with the selection.</param>
/// <param name="QuestionIds">The questions of the section the reference is already applied to.</param>
public sealed record QuestionReferenceNoteOption(Guid Id, string Text, string Html, string LastUpdated, IReadOnlyList<Guid> QuestionIds);

/// <summary>What the dialog is told when it asks for a reference to be saved.</summary>
/// <param name="Succeeded">Whether the reference was saved.</param>
/// <param name="Message">What to tell the user when it was saved; <see langword="null"/> otherwise.</param>
/// <param name="Errors">What is wrong, when it was not saved.</param>
public sealed record QuestionReferenceSaveResponse(bool Succeeded, string? Message, IReadOnlyList<string> Errors);

/// <summary>What the "Add/Select a reference to question" dialog is built from.</summary>
/// <param name="ProfileId">The profile whose section is open.</param>
/// <param name="SectionId">The section whose questions can have references.</param>
/// <param name="NoteTypes">The types of reference that can be added or selected; empty when they could not be loaded.</param>
/// <param name="Questions">The questions of the section, which a reference can also be applied to.</param>
public sealed record QuestionReferenceDialogViewModel(
    Guid ProfileId,
    Guid SectionId,
    IReadOnlyList<ProfileNoteTypeDto> NoteTypes,
    IReadOnlyList<QuestionReferenceChoice> Questions);
