namespace CDC.Web.Models;

/// <summary>One selectable reference-data option for a "List"/"MultiValueList" edit control.</summary>
public sealed record ProfileFieldOptionEditView(Guid Id, string Text, bool IsChecked);

/// <summary>
/// One field rendered under a profile question, either as a real editable control (<see
/// cref="IsEditable"/>) or as a static read-only value, matching the legacy per-datatype
/// controls (<c>BooleanProfileField</c>, <c>DropDownListProfileField</c>,
/// <c>CheckBoxListProfileField</c>, <c>DecimalProfileField</c>, <c>DateProfileField</c>,
/// <c>TextProfileField</c>, <c>LongTextProfileField</c>). A field is only editable for a standard
/// (non-per-species, non-repeating) question, and only when the caller may edit the section.
/// </summary>
public sealed record ProfileFieldEditView
{
    /// <summary>Gets the field identifier, used as the posted form control's name.</summary>
    public required Guid FieldId { get; init; }

    /// <summary>Gets the species the answer is for, when the field belongs to a per-species question.</summary>
    public Guid? SpeciesId { get; init; }

    /// <summary>Gets the posted form control's name, which is also its element id.</summary>
    public string InputName => SpeciesId is null ? $"field_{FieldId}" : $"field_{FieldId}_{SpeciesId:N}";

    /// <summary>Gets the first 50 characters of <see cref="DisplayText"/> as plain text, as the legacy per-species grid showed.</summary>
    public string SummaryText
    {
        get
        {
            var plain = System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Replace(
                DisplayText, "<.*?>", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline, TimeSpan.FromSeconds(1))).Trim();

            return plain.Length > 50 ? plain[..50] : plain;
        }
    }

    /// <summary>Gets the field's recorded answer as text, for a table cell.</summary>
    public string DisplayText => Options.Count > 0 ? CheckedOptionsText() : ValueDisplay ?? string.Empty;

    private string CheckedOptionsText()
    {
        var checkedTexts = Options.Where(option => option.IsChecked).Select(option => option.Text).ToList();
        return checkedTexts.Count > 0 ? string.Join(", ", checkedTexts) : "Not answered";
    }

    /// <summary>Gets the field label.</summary>
    public required string Label { get; init; }

    /// <summary>Gets the data type name, for example <c>Boolean</c>, <c>List</c>,
    /// <c>MultiValueList</c>, <c>Decimal</c>, <c>Date</c>, <c>Long Text</c>, or <c>Text</c>.</summary>
    public required string DataTypeName { get; init; }

    /// <summary>Gets a value indicating whether an answer is required.</summary>
    public bool IsMandatory { get; init; }

    /// <summary>Gets a value indicating whether this field is rendered as a real input control.
    /// <see langword="false"/> for a per-species/repeating question's fields, rendered read-only
    /// instead because editing those is not yet supported, and for a field that is read-only for the
    /// kind of profile (its value comes from elsewhere, so it is never saved or cleared).</summary>
    public bool IsEditable { get; init; }

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

    /// <summary>Gets the name of the control of the same question whose answer decides whether this field is shown, or
    /// <see langword="null"/> when it is always shown.</summary>
    public string? VisibleWhenControl { get; init; }

    /// <summary>Gets the answer of <see cref="VisibleWhenControl"/> that makes this field visible.</summary>
    public Guid VisibleWhenValue { get; init; }
}

/// <summary>One profiled species' answers to a per-species question: a row of the question's table.</summary>
/// <param name="SpeciesId">The species identifier.</param>
/// <param name="SpeciesName">The species name.</param>
/// <param name="Fields">The species' answer to each of the question's fields.</param>
public sealed record ProfileSpeciesAnswerEditView(Guid SpeciesId, string SpeciesName, IReadOnlyList<ProfileFieldEditView> Fields);

/// <summary>One real question (e.g. "2.1 ...") with its editable/read-only fields.</summary>
public sealed record ProfileQuestionEditView
{
    /// <summary>Gets the question's display number (for example "2.1").</summary>
    public required string Number { get; init; }

    /// <summary>Gets the question's display name.</summary>
    public required string Text { get; init; }

    /// <summary>Gets the question's fields.</summary>
    public required IReadOnlyList<ProfileFieldEditView> Fields { get; init; }

    /// <summary>Gets a value indicating whether the question is answered once per profiled species, shown as a table
    /// with a row per species instead of <see cref="Fields"/>.</summary>
    public bool IsPerSpecies { get; init; }

    /// <summary>Gets the table rows of a per-species question.</summary>
    public IReadOnlyList<ProfileSpeciesAnswerEditView> SpeciesRows { get; init; } = [];

    /// <summary>Gets the field whose answer decides whether the question is relevant, or <see cref="Guid.Empty"/>. The page uses
    /// it, with <see cref="IrrelevanceValueId"/> and <see cref="IrrelevanceFlag"/>, to show or hide the question as soon as that
    /// answer changes.</summary>
    public Guid IrrelevanceFieldId { get; init; }

    /// <summary>Gets the controlling list value the relevance test compares against, or <see cref="Guid.Empty"/> for a Yes/No field.</summary>
    public Guid IrrelevanceValueId { get; init; }

    /// <summary>Gets the flag the relevance test applies.</summary>
    public bool IrrelevanceFlag { get; init; }

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
public sealed record QuestionEditAccordionViewModel(string IdPrefix, string EmptyMessage, IReadOnlyList<ProfileQuestionEditView> Questions)
{
    /// <summary>Builds the id of one question's heading in the accordion, which a validation error links to.</summary>
    /// <param name="idPrefix">The accordion's id prefix.</param>
    /// <param name="questionNumber">The question's display number, for example "2.1".</param>
    /// <returns>The heading's element id.</returns>
    public static string HeadingId(string idPrefix, string questionNumber) =>
        $"{idPrefix}-heading-{questionNumber.Replace('.', '-')}";
}
