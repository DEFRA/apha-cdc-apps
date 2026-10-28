using CDC.Common.Comparison;

namespace CDC.Web.Tests.Infrastructure;

public class ProfileVersionComparisonHtmlTests
{
    private static ProfileVersionComparisonReport Report(params ComparisonSection[] sections) =>
        new() { LeftTitle = "Left <1>", RightTitle = "Right 2", Sections = sections };

    [Fact]
    public void Render_EncodesTitlesAndMarksAddedAndRemovedText()
    {
        var question = new ComparisonQuestion("1.1", "Is it <here>?", [
            new ComparisonGroup("Row 1", [
                new ComparisonField("Notes", [new ComparisonSegment("old", ComparisonChange.Removed)], [new ComparisonSegment("new", ComparisonChange.Added), new ComparisonSegment(" <b>")])]),
            new ComparisonGroup(null, [new ComparisonField("Other", [new ComparisonSegment("same")], [new ComparisonSegment("same")])])]);

        var html = ProfileVersionComparisonHtml.Render([Report(new ComparisonSection("1. Intro", [question]))]);

        Assert.Contains("Left &lt;1&gt;", html, StringComparison.Ordinal);
        Assert.Contains("Is it &lt;here&gt;?", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"removed\">old</span>", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"added\">new</span>", html, StringComparison.Ordinal);
        Assert.Contains(" &lt;b&gt;", html, StringComparison.Ordinal);
        Assert.Contains("Row 1", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_SaysSectionsAreIdentical_WhenNothingDiffers()
    {
        var html = ProfileVersionComparisonHtml.Render([Report(new ComparisonSection("1. Intro", []))]);

        Assert.Contains("- Sections are identical -", html, StringComparison.Ordinal);
        Assert.Contains("The two profile versions are identical.", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Render_PutsEachReportInItsOwnBlock()
    {
        var html = ProfileVersionComparisonHtml.Render([Report(), Report()]);

        Assert.Equal(3, html.Split("<div class=\"report\">").Length);
    }
}


