using CDC.Common.Comparison;
using FluentAssertions;

namespace CDC.Api.Tests.ReviewNotifications;

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

        html.Should().Contain("Left &lt;1&gt;").And.Contain("Is it &lt;here&gt;?")
            .And.Contain("<span class=\"removed\">old</span>").And.Contain("<span class=\"added\">new</span>")
            .And.Contain(" &lt;b&gt;").And.Contain("Row 1").And.NotContain("<b>");
    }

    [Fact]
    public void Render_SaysSectionsAreIdentical_WhenNothingDiffers()
    {
        var html = ProfileVersionComparisonHtml.Render([Report(new ComparisonSection("1. Intro", []))]);

        html.Should().Contain("- Sections are identical -").And.Contain("The two profile versions are identical.");
    }

    [Fact]
    public void Render_PutsEachReportInItsOwnBlock()
    {
        var html = ProfileVersionComparisonHtml.Render([Report(), Report()]);

        html.Split("<div class=\"report\">").Length.Should().Be(3);
    }
}
