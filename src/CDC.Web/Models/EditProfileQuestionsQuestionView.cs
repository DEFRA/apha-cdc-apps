namespace CDC.Web.Models;

/// <summary>One selectable option within a "List"/"MultiValueList" type field, with its recorded checked state.</summary>
public sealed record EditProfileQuestionsFieldOptionView(string Text, bool IsChecked);

/// <summary>
/// One answered field rendered under a question on the "Edit profile questions" page. A field
/// with options is rendered as a checkbox group ("List"/"MultiValueList" type); a "Long Text"
/// field's <see cref="ValueDisplay"/> holds rich/HTML content (<see cref="IsHtml"/> is
/// <see langword="true"/>); otherwise it holds the plain-text answer.
/// </summary>
public sealed record EditProfileQuestionsFieldView(
    string Label,
    string? ValueDisplay,
    bool IsHtml,
    IReadOnlyList<EditProfileQuestionsFieldOptionView> Options);

/// <summary>One real question (e.g. "3.2 ...") with its recorded answers, for the "Edit profile questions" page.</summary>
public sealed record EditProfileQuestionsQuestionView(string Number, string Text, IReadOnlyList<EditProfileQuestionsFieldView> Fields);
