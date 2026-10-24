using CDC.Web.Infrastructure;

namespace CDC.Web.Tests.Infrastructure;

public class ProfileQuestionHtmlTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("<p>Is <i>Brucella</i> present?<br></p>\r\n", "Is <i>Brucella</i> present?")]
    [InlineData("A <b>bold</b> <em>word</em>", "A bold <em>word</em>")]
    [InlineData("Line one\r\nline two", "Line oneline two")]
    public void CleanTitle_KeepsOnlyItalics_AndDropsLineBreaks(string? title, string expected) =>
        Assert.Equal(expected, ProfileQuestionHtml.CleanTitle(title));

    [Theory]
    [InlineData(null, 0)]
    [InlineData("", 0)]
    [InlineData("<i>abc</i> d", 5)]
    [InlineData("Fish &amp; chips", 12)]
    public void TitleTextLength_IgnoresMarkup_AndCountsDecodedText(string? title, int expected) =>
        Assert.Equal(expected, ProfileQuestionHtml.TitleTextLength(title));

    [Theory]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    [InlineData("<p>One</p><p>Two</p>", "<p>One</p><p>Two</p>")]
    [InlineData("<ul><li>A</li></ul><b>x</b><u>y</u>", "<ul><li>A</li></ul>xy")]
    [InlineData("a<script>alert(1)</script>b", "ab")]
    [InlineData("a<style>p { color: red }</style>b", "ab")]
    [InlineData("a<SCRIPT type=\"text/javascript\">alert(1)</SCRIPT >b", "ab")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>", "<a>x</a>")]
    [InlineData("<a href=\"mailto:a@example.test\" onclick=\"x()\" title=\"t\">m</a>", "<a href=\"mailto:a@example.test\" title=\"t\">m</a>")]
    [InlineData("  <i>Hello</i>  ", "<i>Hello</i>")]
    public void CleanGuidance_KeepsOnlyTheAllowedMarkup(string? guidance, string expected) =>
        Assert.Equal(expected, ProfileQuestionHtml.CleanGuidance(guidance));

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("<p></p>", false)]
    [InlineData("<p><br></p>", false)]
    [InlineData("<p>Text</p>", true)]
    [InlineData("<ul><li></li></ul>", true)]
    public void HasGuidanceContent_IsTrueOnlyForTextOrAList(string? guidance, bool expected) =>
        Assert.Equal(expected, ProfileQuestionHtml.HasGuidanceContent(guidance));

    [Theory]
    [InlineData(null, "")]
    [InlineData("One\r\nTwo", "One<br/>Two")]
    [InlineData("One\nTwo\rThree", "One<br/>Two<br/>Three")]
    [InlineData("<i>One</i>\r\n<i>Two</i>", "<i>One</i><br/><i>Two</i>")]
    public void ToEditorHtml_ShowsLineBreaksAsBreakTags(string? guidance, string expected) =>
        Assert.Equal(expected, ProfileQuestionHtml.ToEditorHtml(guidance));
}
