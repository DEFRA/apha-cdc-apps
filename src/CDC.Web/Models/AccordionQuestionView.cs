namespace CDC.Web.Models;

/// <summary>One selectable option within a checkbox-group field, with its recorded checked state.</summary>
public sealed record AccordionFieldOptionView(string Text, bool IsChecked);

/// <summary>
/// One answered field rendered under a question in a questionnaire accordion (species data or
/// profile questions). A field with options is rendered as a checkbox group ("List"/"MultiValueList"
/// type); <see cref="IsHtml"/> marks rich/HTML content ("Long Text" type); otherwise
/// <see cref="ValueDisplay"/> holds the plain-text answer.
/// </summary>
public sealed record AccordionFieldView(string Label, string? ValueDisplay, bool IsHtml, IReadOnlyList<AccordionFieldOptionView> Options);

/// <summary>One real question (e.g. "2.1 ...") with its recorded answers, for a questionnaire accordion.</summary>
public sealed record AccordionQuestionView(string Number, string Text, IReadOnlyList<AccordionFieldView> Fields);

/// <summary>Data needed to render the shared <c>_QuestionAccordion</c> partial.</summary>
/// <param name="IdPrefix">Prefix for the accordion's HTML element ids, unique per page.</param>
/// <param name="EmptyMessage">Message shown when there are no questions to render.</param>
/// <param name="Questions">The questions to render.</param>
public sealed record QuestionAccordionViewModel(string IdPrefix, string EmptyMessage, IReadOnlyList<AccordionQuestionView> Questions);
