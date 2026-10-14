namespace CDC.Web.Models;

/// <summary>One selectable option for an editable list/multi-value-list field.</summary>
public sealed record EditableFieldOptionView(Guid Id, string Text, bool IsChecked);

/// <summary>One editable field rendered under a question on the species data edit form.</summary>
public sealed record EditableFieldView(
    Guid FieldId,
    string Label,
    string DataTypeName,
    bool? BooleanValue,
    string? TextValue,
    IReadOnlyList<EditableFieldOptionView> Options);

/// <summary>One question with its editable fields, for the species data edit form.</summary>
public sealed record EditableQuestionView(string Number, string Text, IReadOnlyList<EditableFieldView> Fields);
