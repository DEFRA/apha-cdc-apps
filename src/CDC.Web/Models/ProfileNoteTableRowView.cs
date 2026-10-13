namespace CDC.Web.Models;

/// <summary>One row of the Notes page's sortable, paginated references table.</summary>
/// <param name="ReferenceHtml">The note's rich-text content, rendered unescaped (may contain hyperlinks).</param>
/// <param name="ReferencePlainText">The note's content with HTML tags stripped, used to sort the References column.</param>
/// <param name="QuestionReferenceDisplay">The questions this note was raised against (for example "2.1, 2.3"), used to sort the Question reference column.</param>
public sealed record ProfileNoteTableRowView(string ReferenceHtml, string ReferencePlainText, string QuestionReferenceDisplay);
