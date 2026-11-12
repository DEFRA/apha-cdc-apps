using System.Net;
using System.Text.RegularExpressions;

namespace CDC.Api.Features.ReviewNotifications;

/// <summary>Text helpers shared by the emails composed for GOV.UK Notify.</summary>
internal static partial class NotifyMarkdown
{
    /// <summary>Returns whether <paramref name="value"/> is an absolute http or https URL.</summary>
    /// <param name="value">The configured URL.</param>
    /// <param name="uri">The parsed URL when valid.</param>
    internal static bool TryGetWebUri(string value, out Uri uri) =>
        Uri.TryCreate(value, UriKind.Absolute, out uri!) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    // Titles are stored as HTML (for example <p>Goat Pox</p>), so tags go first, then the characters Notify treats as markup.
    internal static string Clean(string value) =>
        WebUtility.HtmlDecode(HtmlTag().Replace(value, string.Empty))
            .Replace("\r", " ").Replace("\n", " ")
            .Replace("<", string.Empty).Replace(">", string.Empty)
            .Replace("[", string.Empty).Replace("]", string.Empty)
            .Replace("*", string.Empty).Replace("`", string.Empty);

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex HtmlTag();
}
