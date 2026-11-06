using CDC.Web.Infrastructure;

namespace CDC.Web.Tests.Infrastructure;

public class SpeciesCommentHtmlTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \r\n ")]
    public void ToDisplayHtml_IsEmpty_WhenThereIsNoComment(string? comment) =>
        Assert.Equal(string.Empty, SpeciesCommentHtml.ToDisplayHtml(comment));

    // The legacy editor already separates paragraphs with markup, so the line breaks it saved between them must not add blank lines.
    [Fact]
    public void ToDisplayHtml_KeepsTheLegacyEditorsMarkup_WithoutAddingBreaks()
    {
        const string comment = "<p>First &pound;5</p>\r\n<p>Second <em>point</em><br>over two lines</p>\r\n<ul><li>one</li></ul>";

        var html = SpeciesCommentHtml.ToDisplayHtml(comment);

        Assert.Contains("<p>First", html, StringComparison.Ordinal);
        Assert.Contains("<em>point</em><br>over two lines", html, StringComparison.Ordinal);
        Assert.Contains("<ul><li>one</li></ul>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<br/>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void ToDisplayHtml_KeepsTheLineBreaksOfPlainText() =>
        Assert.Equal("one<br/>two<br/>three", SpeciesCommentHtml.ToDisplayHtml("one\r\ntwo\nthree"));

    [Fact]
    public void ToDisplayHtml_EncodesPlainTextThatHoldsMarkupCharacters() =>
        Assert.Equal("5 &lt; 6 &amp; 7 &gt; 3", SpeciesCommentHtml.ToDisplayHtml("5 < 6 & 7 > 3"));

    [Theory]
    [InlineData("<p>ok</p><script>alert(1)</script>", "script")]
    [InlineData("<img src=x onerror=alert(1)>", "onerror")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>", "javascript")]
    [InlineData("<p style=\"color:red\" onclick=\"x()\">x</p>", "onclick")]
    [InlineData("<iframe src=\"https://example.com\"></iframe>", "iframe")]
    public void ToDisplayHtml_RemovesActiveContent(string comment, string forbidden) =>
        Assert.DoesNotContain(forbidden, SpeciesCommentHtml.ToDisplayHtml(comment), StringComparison.OrdinalIgnoreCase);
}
