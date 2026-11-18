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
    // Wrapping paragraphs are stripped before rendering, because the title sits inside a heading and
    // paragraphs are invalid there. Span tags are also removed once their attributes have been
    // filtered out for safety.
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

    // WebUtility.HtmlEncode escapes the ampersand of any character reference the title already contains
    // (the legacy editor stored accented letters, curly quotes and the like as entities such as &rsquo;),
    // so every well-formed named, decimal or hexadecimal reference is restored to render as the character.
    // A reference can only produce text, never markup, so restoring one cannot let anything through.
    [GeneratedRegex(@"&amp;(#[0-9]{1,7}|#[xX][0-9a-fA-F]{1,6}|[a-zA-Z][a-zA-Z0-9]{1,30});", RegexOptions.None, matchTimeoutMilliseconds: 2000)]
    private static partial Regex CharacterReferenceRegex();

    // Pasting from a browser or Word can leave comments such as <!--StartFragment--> in a stored title.
    // They are not text, so they are removed rather than shown.
    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline, matchTimeoutMilliseconds: 2000)]
    private static partial Regex CommentRegex();

    [GeneratedRegex(@"&lt;/?span\b(?:(?!&gt;).)*&gt;", RegexOptions.IgnoreCase)]
    private static partial Regex SpanTagRegex();

    [GeneratedRegex(@"<[^>]*>")]
    private static partial Regex AnyTagRegex();

    /// <summary>Encodes <paramref name="title"/> and selectively restores only the allowed formatting tags and character references.</summary>
    public static IHtmlContent Format(string? title)
    {
        if (string.IsNullOrEmpty(title))
        {
            return HtmlString.Empty;
        }

        var encoded = WebUtility.HtmlEncode(CommentRegex().Replace(title, string.Empty));

        encoded = SpanTagRegex().Replace(encoded, string.Empty);

        foreach (var tag in StrippedTags)
        {
            encoded = encoded.Replace($"&lt;{tag}&gt;", string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        foreach (var tag in AllowedTags)
        {
            encoded = encoded.Replace($"&lt;{tag}&gt;", $"<{tag}>", StringComparison.OrdinalIgnoreCase);
        }

        return new HtmlString(CharacterReferenceRegex().Replace(encoded, "&$1;"));
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
