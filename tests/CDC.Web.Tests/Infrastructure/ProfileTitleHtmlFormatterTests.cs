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
    [InlineData("Farmer&rsquo;s pigs", "Farmer&rsquo;s pigs")]
    [InlineData("&ldquo;Quoted&rdquo; &ndash; &pound;5", "&ldquo;Quoted&rdquo; &ndash; &pound;5")]
    [InlineData("Caf&eacute; &iuml;", "Caf&eacute; &iuml;")]
    [InlineData("Fish &amp; chips", "Fish &amp; chips")]
    [InlineData("Quote&#8217;s and &#x2019;s", "Quote&#8217;s and &#x2019;s")]
    [InlineData("AT&T and R&D; more", "AT&amp;T and R&amp;D; more")]
    [InlineData("a & b", "a &amp; b")]
    [InlineData("&; &#; &#x; &1a;", "&amp;; &amp;#; &amp;#x; &amp;1a;")]
    public void Format_ShowsAWellFormedCharacterReferenceAsTheCharacter_AndEscapesEveryOtherAmpersand(string title, string expected) =>
        Assert.Equal(expected, ProfileTitleHtmlFormatter.Format(title).ToString());

    [Fact]
    public void Format_DoesNotTurnReferencesToMarkupIntoMarkup()
    {
        var rendered = ProfileTitleHtmlFormatter.Format("&lt;script&gt;alert(1)&lt;/script&gt; &#60;b&#62;").ToString();

        Assert.DoesNotContain("<script>", rendered);
        Assert.DoesNotContain("<b>", rendered);
        Assert.Equal("&lt;script&gt;alert(1)&lt;/script&gt; &#60;b&#62;", rendered);
    }

    [Fact]
    public void Format_RemovesCommentsLeftByPastingFromAnotherPage()
    {
        var result = ProfileTitleHtmlFormatter.Format(
            "<!--StartFragment--><!-- x-tinymce/html -->How well do you <em>know</em>?<!--EndFragment-->");

        Assert.Equal("How well do you <em>know</em>?", result.ToString());
    }

    [Fact]
    public void Format_ReturnsEmpty_WhenTheTitleIsOnlyAComment()
    {
        var result = ProfileTitleHtmlFormatter.Format("<!--StartFragment-->");

        Assert.Equal(string.Empty, result.ToString());
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

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ToPlainText_ReturnsEmpty_ForNullOrEmptyTitle(string? title)
    {
        Assert.Equal(string.Empty, ProfileTitleHtmlFormatter.ToPlainText(title));
    }

    [Fact]
    public void ToPlainText_ReturnsTitle_Unchanged_WhenItHasNoMarkup()
    {
        Assert.Equal("Bovine tuberculosis", ProfileTitleHtmlFormatter.ToPlainText("Bovine tuberculosis"));
    }

    [Fact]
    public void ToPlainText_StripsWrappingParagraphTag()
    {
        Assert.Equal(
            "Leptospirosis (Weil's Disease)",
            ProfileTitleHtmlFormatter.ToPlainText("<p>Leptospirosis (Weil's Disease)</p>"));
    }

    [Fact]
    public void ToPlainText_StripsEveryTag_IncludingNestedInlineFormatting()
    {
        Assert.Equal(
            "E. coli (ESBLs)",
            ProfileTitleHtmlFormatter.ToPlainText("<p><em>E. coli </em>(ESBLs)</p>"));
    }

    [Fact]
    public void ToPlainText_DecodesHtmlEntities()
    {
        Assert.Equal(
            "Fasciola hepatica\u00A0(Liver Fluke)",
            ProfileTitleHtmlFormatter.ToPlainText("Fasciola hepatica&nbsp;(Liver Fluke)"));
    }
}
