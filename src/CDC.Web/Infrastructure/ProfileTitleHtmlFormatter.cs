using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Profile titles may contain a small set of inline formatting tags (e.g. scientific names in
/// <c>&lt;em&gt;</c>) authored via the legacy profile editor. Renders those specific tags as HTML
/// while HTML-encoding everything else, so no other markup or attributes can be injected.
/// </summary>
public static partial class ProfileTitleHtmlFormatter
{
    // <p> and <span> wrappers are stripped rather than rendered: titles are shown inside heading
    // elements (where <p> is invalid block-level content), and <span> carries no visual formatting
    // of its own once its attributes (e.g. style, data-*) are removed for safety.
    private static readonly string[] StrippedTags = ["p", "/p"];

    private static readonly string[] AllowedTags =
    [
        "em", "/em",
        "strong", "/strong",
        "i", "/i",
        "b", "/b",
        "sup", "/sup",
        "sub", "/sub",
        "br", "br/", "br /"
    ];

    // Named character references (e.g. &nbsp;) get double-encoded by HtmlEncode into &amp;nbsp;;
    // restore the ones with no markup risk so they render as the intended character.
    private static readonly string[] AllowedEntities = ["nbsp;", "lt;", "gt;", "quot;", "apos;", "#39;"];

    [GeneratedRegex(@"&lt;/?span\b(?:(?!&gt;).)*&gt;", RegexOptions.IgnoreCase)]
    private static partial Regex SpanTagRegex();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex AnyTagRegex();

    /// <summary>Encodes <paramref name="title"/> and selectively restores only the allowed formatting tags.</summary>
    public static IHtmlContent Format(string? title)
    {
        if (string.IsNullOrEmpty(title))
        {
            return HtmlString.Empty;
        }

        var encoded = WebUtility.HtmlEncode(title);

        encoded = SpanTagRegex().Replace(encoded, string.Empty);

        foreach (var tag in StrippedTags)
        {
            encoded = encoded.Replace($"&lt;{tag}&gt;", string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var tag in AllowedTags)
        {
            encoded = encoded.Replace($"&lt;{tag}&gt;", $"<{tag}>", StringComparison.OrdinalIgnoreCase);
        }

        foreach (var entity in AllowedEntities)
        {
            encoded = encoded.Replace($"&amp;{entity}", $"&{entity}", StringComparison.OrdinalIgnoreCase);
        }

        return new HtmlString(encoded);
    }

    /// <summary>Strips every markup tag from <paramref name="title"/> and decodes entities, leaving
    /// only the text a user would actually read - for matching/sorting/filtering, never for display.</summary>
    public static string ToPlainText(string? title)
    {
        if (string.IsNullOrEmpty(title))
        {
            return string.Empty;
        }

        var withoutTags = AnyTagRegex().Replace(title, string.Empty);

        return WebUtility.HtmlDecode(withoutTags).Trim();
    }
}
