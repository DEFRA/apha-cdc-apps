using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;

namespace CDC.Web.Models;

/// <summary>One selectable reference-data option for a "List"/"MultiValueList" edit control.</summary>
public sealed record ProfileFieldOptionEditView(Guid Id, string Text, bool IsChecked);

/// <summary>
/// One field rendered under a profile question, either as a real editable control (<see
/// cref="IsEditable"/>) or as a static read-only value, matching the legacy per-datatype
/// controls (<c>BooleanProfileField</c>, <c>DropDownListProfileField</c>,
/// <c>CheckBoxListProfileField</c>, <c>DecimalProfileField</c>, <c>DateProfileField</c>,
/// <c>TextProfileField</c>, <c>LongTextProfileField</c>). A field is only editable when the caller
/// may edit the section. For a per-species or a repeating question the view holds the field's definition for the panel that edits one
/// answer at a time, and the answers themselves are in <see cref="ProfileSpeciesRowView"/> or <see cref="ProfileQuestionRowView"/>.
/// </summary>
public sealed record ProfileFieldEditView
{
    /// <summary>Gets the field identifier, used as the posted form control's name.</summary>
    public required Guid FieldId { get; init; }

    /// <summary>Gets the field label. Many of the questionnaire's fields have none, the question's text being what they answer.</summary>
    public required string Label { get; init; }

    /// <summary>Gets what a screen reader reads for the field's control when <see cref="Label"/> is empty, for example "Answer to question 6.1".</summary>
    public string LabelFallback { get; init; } = string.Empty;

    /// <summary>Gets the data type name, for example <c>Boolean</c>, <c>List</c>,
    /// <c>MultiValueList</c>, <c>Decimal</c>, <c>Date</c>, <c>Long Text</c>, or <c>Text</c>.</summary>
    public required string DataTypeName { get; init; }

    /// <summary>Gets a value indicating whether an answer is required.</summary>
    public bool IsMandatory { get; init; }

    /// <summary>Gets a value indicating whether this field is rendered as a real input control.
    /// <see langword="false"/> for a per-species or repeating field of a type the panel cannot edit, and for a field that is
    /// read-only for the kind of profile (its value comes from elsewhere, so it is never saved or cleared).</summary>
    public bool IsEditable { get; init; }

    /// <summary>Gets a value indicating whether a per-species or repeating question's table has a column for the field.</summary>
    public bool IncludeInSummary { get; init; }

    /// <summary>Gets the numbers of the questions (for example <c>5.2</c>) that the answer to the field can make irrelevant. The page asks
    /// the user to confirm a change to such an answer, as the legacy page did.</summary>
    public IReadOnlyList<string> AffectsRelevanceOf { get; init; } = [];

    /// <summary>Gets the question numbers in <see cref="AffectsRelevanceOf"/>, separated by commas, as the confirmation lists them; empty when
    /// the field controls no question.</summary>
    public string AffectsRelevanceText => string.Join(", ", AffectsRelevanceOf);

    /// <summary>Gets the attribute that marks a control whose change is confirmed, naming the questions it may affect, for a view to write
    /// among a control's attributes. It is nothing when the field controls no question: Razor writes a <c>data-</c> attribute with no value
    /// as an empty one, which the script would take for a field that controls questions.</summary>
    public IHtmlContent RelevanceAttribute => AffectsRelevanceOf.Count == 0
        ? HtmlString.Empty
        : new HtmlString($"data-relevance-affects=\"{HtmlEncoder.Default.Encode(AffectsRelevanceText)}\"");

    /// <summary>Gets the read-only plain-text or rich/HTML display value, used when not <see cref="IsEditable"/>.</summary>
    public string? ValueDisplay { get; init; }

    /// <summary>Gets a value indicating whether <see cref="ValueDisplay"/> is rich/HTML content.</summary>
    public bool IsHtml { get; init; }

    /// <summary>Gets the recorded answer for a "Boolean" field, to pre-select the Yes/No control.</summary>
    public bool? BooleanValue { get; init; }

    /// <summary>Gets the recorded answer for a "Decimal" field.</summary>
    public decimal? DecimalValue { get; init; }

    /// <summary>Gets the recorded answer for a "Date" field.</summary>
    public DateTime? DateValue { get; init; }

    /// <summary>Gets the recorded answer for a "Text"/"Long Text" field.</summary>
    public string? TextValue { get; init; }

    /// <summary>Gets the reference data options for a "List"/"MultiValueList" field, with their
    /// recorded checked state.</summary>
    public IReadOnlyList<ProfileFieldOptionEditView> Options { get; init; } = [];

    /// <summary>Gets the numbers of the questions (for example "4.2, 4.3") whose relevance depends on this field's answer,
    /// or <see langword="null"/> when it affects none; changing the answer asks for confirmation first.</summary>
    public string? AffectsRelevanceOf { get; init; }
}

/// <summary>One answer to one field of a per-species or repeating question: a profiled species' answer, or a row's.</summary>
/// <param name="FieldId">The field answered.</param>
/// <param name="FormValue">The value the page keeps in a hidden input and posts back with the section's answers.</param>
/// <param name="SummaryText">The text the question's table shows for the answer.</param>
public sealed record ProfileAnswerView(Guid FieldId, string FormValue, string SummaryText);

/// <summary>One profiled species' row of a per-species question's table, with its answer to each of the question's fields.</summary>
public sealed record ProfileSpeciesRowView
{
    /// <summary>Gets the species identifier.</summary>
    public required Guid SpeciesId { get; init; }

    /// <summary>Gets the species name, which may contain italics markup.</summary>
    public required string Name { get; init; }

    /// <summary>Gets a value indicating whether the question applies to the species. The table leaves out a species the answers make the
    /// question irrelevant for, but keeps its answers on the page so that the form still posts them.</summary>
    public bool IsRelevant { get; init; } = true;

    /// <summary>Gets the species' answer to each field of the question.</summary>
    public required IReadOnlyList<ProfileAnswerView> Answers { get; init; }

    /// <summary>Builds the name of the hidden input that holds a species' answer to a field, which the save reads the answer from.</summary>
    /// <param name="speciesId">The species.</param>
    /// <param name="fieldId">The field.</param>
    /// <returns>The input's name.</returns>
    public static string InputName(Guid speciesId, Guid fieldId) => $"species_{speciesId}_field_{fieldId}";

    /// <summary>Finds the species' answer to a field.</summary>
    /// <param name="fieldId">The field.</param>
    /// <returns>The answer, or <see langword="null"/> when the field is not one of the question's.</returns>
    public ProfileAnswerView? AnswerTo(Guid fieldId) => Answers.FirstOrDefault(answer => answer.FieldId == fieldId);
}

/// <summary>One row of a repeating question's table (the legacy <c>QuestionRow</c>), with its answer to each of the question's fields.</summary>
public sealed record ProfileQuestionRowView
{
    /// <summary>Gets the row identifier: that of the recorded row, or <see cref="Guid.Empty"/> for the blank row the page copies when the user adds one.</summary>
    public required Guid RowId { get; init; }

    /// <summary>Gets the row's answer to each field of the question.</summary>
    public required IReadOnlyList<ProfileAnswerView> Answers { get; init; }

    /// <summary>Builds the name of the hidden input that lists a question's rows, one value for each, in the order the question keeps them.
    /// It is always posted for a question the user may edit, with an empty value first, so that a question whose rows were all deleted is
    /// told from one the page did not offer.</summary>
    /// <param name="questionId">The repeating question.</param>
    /// <returns>The input's name.</returns>
    public static string RowsInputName(Guid questionId) => $"rows_{questionId}";

    /// <summary>Builds the name of the hidden input that holds a row's answer to a field, which the save reads the answer from.</summary>
    /// <param name="rowId">The row.</param>
    /// <param name="fieldId">The field.</param>
    /// <returns>The input's name.</returns>
    public static string InputName(Guid rowId, Guid fieldId) => $"row_{rowId}_field_{fieldId}";

    /// <summary>Finds the row's answer to a field.</summary>
    /// <param name="fieldId">The field.</param>
    /// <returns>The answer, or <see langword="null"/> when the field is not one of the question's.</returns>
    public ProfileAnswerView? AnswerTo(Guid fieldId) => Answers.FirstOrDefault(answer => answer.FieldId == fieldId);
}

/// <summary>Data needed to render the <c>_ProfileAnswerFieldEdit</c> partial: one field's control in the panel that edits one answer of a per-species or repeating question.</summary>
/// <param name="IdPrefix">Prefix for the control's HTML element ids, unique to the question.</param>
/// <param name="Field">The field to render. A control that is not <see cref="ProfileFieldEditView.IsEditable"/> is shown disabled.</param>
public sealed record ProfileAnswerFieldEditModel(string IdPrefix, ProfileFieldEditView Field)
{
    /// <summary>Builds the id of the field's control, which its label points at.</summary>
    public string ControlId => $"{IdPrefix}-field-{Field.FieldId}";
}

/// <summary>Data needed to render the <c>_ProfileRepeatingRow</c> partial: one row of a repeating question's table.</summary>
/// <param name="Question">The repeating question.</param>
/// <param name="Row">The row to render: a recorded one, or the blank row the page copies when the user adds one.</param>
/// <param name="Position">The one-based position of the row in the table, which its links name it by.</param>
public sealed record ProfileRepeatingRowModel(ProfileQuestionEditView Question, ProfileQuestionRowView Row, int Position);

/// <summary>One real question (e.g. "2.1 ...") with its editable/read-only fields.</summary>
public sealed record ProfileQuestionEditView
{
    /// <summary>Gets the profiled species' rows when the question is answered once for each species; <see langword="null"/> for any other
    /// question. It is empty when the profile has no species to answer for.</summary>
    public IReadOnlyList<ProfileSpeciesRowView>? SpeciesRows { get; init; }

    /// <summary>Gets a value indicating whether the user may change a species' answers (the legacy <c>Edit</c> link) rather than only
    /// look at them (the legacy <c>View</c> link): they may edit the section and at least one field is not read-only.</summary>
    public bool CanEditSpecies { get; init; }

    /// <summary>Gets the rows recorded for a repeating question, in the order the question keeps them; <see langword="null"/> for any other
    /// question. It is empty when none has been recorded.</summary>
    public IReadOnlyList<ProfileQuestionRowView>? Rows { get; init; }

    /// <summary>Gets a value indicating whether the user may add, change and delete a repeating question's rows (the legacy <c>Add</c>, <c>Edit</c>
    /// and <c>Delete</c> links) rather than only look at them (the legacy <c>View</c> link): they may edit the section and at least one
    /// field is not read-only.</summary>
    public bool CanEditRows { get; init; }

    /// <summary>Gets the question's identifier, which the edit references and help icons name when they open their dialogs.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the question's display number (for example "2.1").</summary>
    public required string Number { get; init; }

    /// <summary>Gets the question's display name.</summary>
    public required string Text { get; init; }

    /// <summary>Gets the question's fields.</summary>
    public required IReadOnlyList<ProfileFieldEditView> Fields { get; init; }

    /// <summary>Gets whether the question has no validation errors, for the tick/cross status icon matching the legacy
    /// <c>CollapsiblePanel</c>'s <c>IsValid</c> icon (legacy <c>question.Validate().Count = 0</c>), or
    /// <see langword="null"/> when the section's validation status could not be loaded, which shows no icon.</summary>
    public bool? IsValid { get; init; }

    /// <summary>Gets a value indicating whether the "Edit references" icon is shown, matching the
    /// legacy <c>CollapsiblePanel.HasNoteLink</c>/<c>NoteList.CanEditNoteList()</c> rule: shown
    /// only when the caller may edit the section.</summary>
    public bool CanEditReferences { get; init; }

    /// <summary>Gets a value indicating whether the answers make the question irrelevant, so it is not shown (the legacy page
    /// hid such a question and cleared its answers when the section was saved). Its fields stay on the page so the
    /// form still posts them.</summary>
    public bool IsHidden { get; init; }
}

/// <summary>Data needed to render the <c>_ProfileQuestionEditAccordion</c> partial.</summary>
/// <param name="IdPrefix">Prefix for the accordion's HTML element ids, unique per page.</param>
/// <param name="EmptyMessage">Message shown when there are no questions to render.</param>
/// <param name="Questions">The questions to render.</param>
/// <param name="CollapseOnOpen">Whether every question opens collapsed, whatever the accordion remembered of an earlier visit.</param>
public sealed record QuestionEditAccordionViewModel(
    string IdPrefix,
    string EmptyMessage,
    IReadOnlyList<ProfileQuestionEditView> Questions,
    bool CollapseOnOpen = false)
{
    /// <summary>Builds the id of one question's heading in the accordion, which a validation error links to.</summary>
    /// <param name="idPrefix">The accordion's id prefix.</param>
    /// <param name="questionNumber">The question's display number, for example "2.1".</param>
    /// <returns>The heading's element id.</returns>
    public static string HeadingId(string idPrefix, string questionNumber) =>
        $"{idPrefix}-heading-{questionNumber.Replace('.', '-')}";
}
