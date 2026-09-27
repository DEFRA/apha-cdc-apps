using CDC.Web.Infrastructure;

namespace CDC.Web.Tests.Infrastructure;

public class ProfileTitleHtmlFormatterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Format_ReturnsEmpty_ForNullOrEmptyTitle(string? title)
    {
        var result = ProfileTitleHtmlFormatter.Format(title);

        Assert.Equal(string.Empty, result.ToString());
    }

    [Fact]
    public void Format_RendersPlainText_Unchanged()
    {
        var result = ProfileTitleHtmlFormatter.Format("Bovine tuberculosis");

        Assert.Equal("Bovine tuberculosis", result.ToString());
    }

    [Fact]
    public void Format_StripsWrappingParagraphTag_AndRendersInlineEmphasis()
    {
        var result = ProfileTitleHtmlFormatter.Format("<p><em>E. coli </em>(ESBLs)</p>");

        Assert.Equal("<em>E. coli </em>(ESBLs)", result.ToString());
    }

    [Fact]
    public void Format_StripsSpanTagsAndAttributes_RegardlessOfNesting()
    {
        var result = ProfileTitleHtmlFormatter.Format(
            "<span data-teams=\"true\"><span style=\"font-size: inherit;\">Highly Pathogenic Avian Influenza</span> (HPAI)</span>");

        Assert.Equal("Highly Pathogenic Avian Influenza (HPAI)", result.ToString());
    }

    [Fact]
    public void Format_RendersNbspEntity_AsNonBreakingSpace_NotLiteralText()
    {
        var result = ProfileTitleHtmlFormatter.Format("<em>Fasciola hepatica&nbsp;</em>(Liver Fluke)");

        Assert.Equal("<em>Fasciola hepatica&nbsp;</em>(Liver Fluke)", result.ToString());
    }

    [Theory]
    [InlineData("<strong>Anthrax</strong>", "<strong>Anthrax</strong>")]
    [InlineData("<i>Anthrax</i>", "<i>Anthrax</i>")]
    [InlineData("<b>Anthrax</b>", "<b>Anthrax</b>")]
    [InlineData("Species<sup>1</sup>", "Species<sup>1</sup>")]
    [InlineData("Species<sub>1</sub>", "Species<sub>1</sub>")]
    [InlineData("Line one<br>Line two", "Line one<br>Line two")]
    public void Format_RendersAllowedInlineTags(string title, string expected)
    {
        var result = ProfileTitleHtmlFormatter.Format(title);

        Assert.Equal(expected, result.ToString());
    }

    [Fact]
    public void Format_EncodesDisallowedTagsAndAttributes_PreventingXss()
    {
        var result = ProfileTitleHtmlFormatter.Format("<script>alert('xss')</script><em onclick=\"alert('xss')\">Anthrax</em>");

        var rendered = result.ToString();

        Assert.DoesNotContain("<script>", rendered);
        Assert.DoesNotContain("<em onclick=", rendered);
        Assert.Contains("&lt;script&gt;", rendered);
        Assert.Contains("&lt;em onclick=", rendered);
    }
}
