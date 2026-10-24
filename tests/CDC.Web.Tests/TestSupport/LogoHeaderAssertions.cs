using System.Text.RegularExpressions;

namespace CDC.Web.Tests.TestSupport;

/// <summary>Checks a report's HTML for the legacy first-page header: the APHA logo, the D2R2 logo and, on some reports, the title between them.</summary>
internal static class LogoHeaderAssertions
{
    private const string HeaderStart = "<header class=\"logo-header";
    private const string PngInBase64 = "iVBORw0KGgo";

    /// <summary>Asserts the HTML holds one logo header with both logos, and a title only when <paramref name="encodedTitle"/> is given.</summary>
    /// <param name="html">The report's HTML.</param>
    /// <param name="encodedTitle">The HTML-encoded title the header prints, or <see langword="null"/> for a header with only the logos.</param>
    public static void AssertOnce(string html, string? encodedTitle = null)
    {
        Assert.Equal(1, Regex.Count(html, HeaderStart));
        Assert.Contains(encodedTitle is null ? $"{HeaderStart}\">" : $"{HeaderStart} logo-header--titled\">", html);
        Assert.Contains("alt=\"Animal and Plant Health Agency\"", html);
        Assert.Contains("alt=\"D2R2\"", html);
        Assert.Equal(2, Regex.Count(html, $"src=\"data:image/png;base64,{PngInBase64}"));

        if (encodedTitle is null)
        {
            Assert.DoesNotContain("<h1 class=\"logo-header__title\">", html);
        }
        else
        {
            Assert.Contains($"<h1 class=\"logo-header__title\">{encodedTitle}</h1>", html);
        }
    }

    /// <summary>Asserts the HTML holds no logo header.</summary>
    /// <param name="html">The report's HTML.</param>
    public static void AssertNone(string html) => Assert.DoesNotContain(HeaderStart, html);
}
