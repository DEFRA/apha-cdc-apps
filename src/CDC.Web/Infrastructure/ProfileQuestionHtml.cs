using System.Text.RegularExpressions;
using Ganss.Xss;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Cleans the text of a profile question before it is saved. Question titles are single-line italic text; the
/// guidance is entered through a rich text editor (italics, lists and links), so both are untrusted input.
/// </summary>
public static partial class ProfileQuestionHtml
{
    /// <summary>The longest plain-text question title accepted, which is as much as the legacy page allowed.</summary>
    public const int MaxTitleTextLength = 255;

    private static readonly string[] GuidanceTags = ["a", "br", "em", "i", "li", "ol", "p", "ul"];
    private static readonly string[] GuidanceAttributes = ["href", "target", "title"];
    private static readonly string[] GuidanceSchemes = ["http", "https", "mailto"];

    /// <summary>Keeps only italics and drops paragraph and line-break markup, as the legacy page did before saving a title.</summary>
    /// <param name="title">The submitted title.</param>
    /// <returns>The title to save.</returns>
    public static string CleanTitle(string? title) => SpeciesNameHtml.Clean(title);

    /// <summary>Counts the characters a reader sees in a title, ignoring markup.</summary>
    /// <param name="title">The title, which may hold italic markup.</param>
    /// <returns>The length of the text without markup.</returns>
    public static int TitleTextLength(string? title) => ProfileTitleHtmlFormatter.ToPlainText(title).Length;

    /// <summary>Removes any markup that is not italics, a list, a paragraph, a line break or a safe link.</summary>
    /// <param name="guidance">The submitted guidance.</param>
    /// <returns>The guidance to save.</returns>
    public static string CleanGuidance(string? guidance)
    {
        if (string.IsNullOrWhiteSpace(guidance))
        {
            return string.Empty;
        }

        var sanitizer = new HtmlSanitizer { KeepChildNodes = true };
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedCssProperties.Clear();

        foreach (var tag in GuidanceTags)
        {
            sanitizer.AllowedTags.Add(tag);
        }

        foreach (var attribute in GuidanceAttributes)
        {
            sanitizer.AllowedAttributes.Add(attribute);
        }

        foreach (var scheme in GuidanceSchemes)
        {
            sanitizer.AllowedSchemes.Add(scheme);
        }

        return sanitizer.Sanitize(ActiveContentPattern().Replace(guidance, string.Empty)).Trim();
    }

    /// <summary>
    /// Tells whether guidance holds anything a reader would see. An editor left empty can still submit empty
    /// paragraphs, and a list item counts even when it has no text yet.
    /// </summary>
    /// <param name="guidance">The guidance, after <see cref="CleanGuidance"/>.</param>
    /// <returns><see langword="true"/> when there is text or a list to show.</returns>
    public static bool HasGuidanceContent(string? guidance) =>
        !string.IsNullOrWhiteSpace(guidance)
        && (ProfileTitleHtmlFormatter.ToPlainText(guidance).Length > 0 || guidance.Contains("<li", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Prepares stored guidance for the editor. Older guidance holds raw line breaks, which an HTML editor would
    /// collapse, so they become <c>&lt;br/&gt;</c> as the legacy page did.
    /// </summary>
    /// <param name="guidance">The stored guidance.</param>
    /// <returns>The guidance to show in the editor.</returns>
    public static string ToEditorHtml(string? guidance) => LineBreakPattern().Replace(guidance ?? string.Empty, "<br/>");

    [GeneratedRegex(@"\r\n|\r|\n")]
    private static partial Regex LineBreakPattern();

    // The sanitiser keeps the text of a tag it removes, which suits a stray bold tag but not a script's body.
    // Each element is spelled out because the source generator cannot emit a case-insensitive backreference.
    [GeneratedRegex(@"<script\b.*?</script\s*>|<style\b.*?</style\s*>", RegexOptions.IgnoreCase | RegexOptions.Singleline, matchTimeoutMilliseconds: 2000)]
    private static partial Regex ActiveContentPattern();
}
