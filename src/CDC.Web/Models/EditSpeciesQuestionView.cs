namespace CDC.Web.Models;

/// <summary>One selectable option within a "List" type field, with its recorded checked state.</summary>
public sealed record EditSpeciesFieldOptionView(string Text, bool IsChecked);

/// <summary>
/// One answered field rendered under a question in the species data accordion. A field with
/// options is rendered as a checkbox group ("List" type); otherwise <see cref="ValueDisplay"/>
/// holds the plain-text answer (boolean/text types).
/// </summary>
public sealed record EditSpeciesFieldView(string Label, string? ValueDisplay, IReadOnlyList<EditSpeciesFieldOptionView> Options);

/// <summary>One real question (e.g. "2.1 ...") with its recorded answers, for the species data accordion.</summary>
public sealed record EditSpeciesQuestionView(string Number, string Text, IReadOnlyList<EditSpeciesFieldView> Fields);
