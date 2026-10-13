using System.Text.RegularExpressions;
using Ganss.Xss;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Sanitises the HTML of a review comment, which is entered through a link-only rich text editor
/// and is therefore untrusted. Only basic text markup and http/https/mailto links survive.
/// </summary>
public static partial class ReviewCommentHtml
{
    private static readonly string[] AllowedTags = ["a", "b", "br", "em", "i", "li", "ol", "p", "strong", "u", "ul"];
    private static readonly string[] AllowedAttributes = ["href", "title"];
    private static readonly string[] AllowedSchemes = ["http", "https", "mailto"];

    /// <summary>Removes any markup that is not basic text formatting or a safe link.</summary>
    /// <param name="html">The submitted comment HTML (or plain text).</param>
    /// <returns>The sanitised HTML.</returns>
    public static string Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

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

        return sanitizer.Sanitize(html);
    }

    /// <summary>Sanitises a comment for display, turning line breaks in plain-text comments into
    /// <c>&lt;br/&gt;</c> as the legacy page did.</summary>
    /// <param name="html">The stored comment.</param>
    /// <returns>HTML safe to render.</returns>
    public static string ToDisplayHtml(string? html) =>
        LineBreakPattern().Replace(Sanitize(html), "<br/>");

    [GeneratedRegex(@"\r\n|\r|\n")]
    private static partial Regex LineBreakPattern();
}
