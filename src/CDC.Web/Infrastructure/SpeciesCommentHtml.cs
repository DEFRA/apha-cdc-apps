using System.Text.RegularExpressions;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Prepares a species' stored "Supporting comments" for display. The legacy rich text editor saved them as simple
/// HTML, which is sanitised; text typed into a plain edit box has no markup, so its line breaks are kept.
/// </summary>
public static partial class SpeciesCommentHtml
{
    /// <summary>Sanitises a stored comment for display.</summary>
    /// <param name="comment">The stored comment, as HTML or plain text.</param>
    /// <returns>HTML safe to render, or an empty string when there is no comment.</returns>
    public static string ToDisplayHtml(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return string.Empty;
        }

        var html = ReviewCommentHtml.Sanitize(comment);

        return MarkupPattern().IsMatch(comment) ? html : LineBreakPattern().Replace(html, "<br/>");
    }

    [GeneratedRegex(@"</?[a-z]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex MarkupPattern();

    [GeneratedRegex(@"\r\n|\r|\n", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex LineBreakPattern();
}
