using System.Text.RegularExpressions;
using Ganss.Xss;
using Microsoft.AspNetCore.Html;

namespace CDC.Web.Infrastructure.Reports;

/// <summary>
/// Prepares the rich text of a profile answer or reference for printing: the legacy <c>TextDataParagraph</c>
/// stripped the wrapping paragraph, kept links readable, and sanitised the HTML before it reached the PDF.
/// </summary>
public static partial class ReportRichText
{
    private static readonly string[] AllowedTags =
    [
        "a", "b", "br", "em", "i", "li", "ol", "p", "strong", "sub", "sup", "u", "ul",
        "table", "thead", "tbody", "tr", "td", "th", "h1", "h2", "h3", "h4", "h5", "h6", "span"
    ];

    private static readonly string[] AllowedAttributes = ["href", "title", "colspan", "rowspan"];
    private static readonly string[] AllowedSchemes = ["http", "https", "mailto"];

    /// <summary>Gets the line shown when a text answer is empty.</summary>
    public const string NoDataText = "- No data has been entered -";

    /// <summary>Sanitises rich text for printing.</summary>
    /// <param name="html">The stored text; plain text and HTML are both accepted.</param>
    /// <returns>HTML safe to print, or empty content when <paramref name="html"/> is blank.</returns>
    public static IHtmlContent ToHtml(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return HtmlString.Empty;
        }

        var text = ParagraphBreakPattern().Replace(html.Trim(), "<br/><br/>");
        text = OuterParagraphPattern().Replace(text, string.Empty);
        text = text.Replace("</a><em>", "</a>&nbsp;<em>", StringComparison.Ordinal).Replace("</a> <em>", "</a>&nbsp;<em>", StringComparison.Ordinal);
        text = LineBreakPattern().Replace(text, "<br/>");

        return new HtmlString(CreateSanitizer().Sanitize(text));
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedCssProperties.Clear();

        foreach (var tag in AllowedTags)
        {
            sanitizer.AllowedTags.Add(tag);
        }

        foreach (var attribute in AllowedAttributes)
        {
            sanitizer.AllowedAttributes.Add(attribute);
        }

        foreach (var scheme in AllowedSchemes)
        {
            sanitizer.AllowedSchemes.Add(scheme);
        }

        return sanitizer;
    }

    [GeneratedRegex(@"</p>\s*<p\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex ParagraphBreakPattern();

    [GeneratedRegex(@"^\s*<p\b[^>]*>|</p>\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex OuterParagraphPattern();

    [GeneratedRegex(@"\r\n|\r|\n")]
    private static partial Regex LineBreakPattern();
}
