using System.Text.RegularExpressions;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Species names may carry <c>&lt;em&gt;</c>/<c>&lt;i&gt;</c> italics, as the legacy single-line editor allowed.
/// </summary>
public static partial class SpeciesNameHtml
{
    [GeneratedRegex(@"<(?!/?(?:em|i)>)[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex DisallowedTagRegex();

    [GeneratedRegex(@"[\r\n]+")]
    private static partial Regex LineBreakRegex();

    /// <summary>Removes every tag except italics and any line breaks, then trims.</summary>
    public static string Clean(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return string.Empty;
        }

        return LineBreakRegex().Replace(DisallowedTagRegex().Replace(name, string.Empty), string.Empty).Trim();
    }

    /// <summary>The name as a reader sees it, with all markup removed.</summary>
    public static string ToPlainText(string? name) => ProfileTitleHtmlFormatter.ToPlainText(name);
}
