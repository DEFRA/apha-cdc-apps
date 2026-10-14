using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class TextDiffTests
{
    private static string Marked(IReadOnlyList<ComparisonSegment> segments, ComparisonChange change) =>
        string.Concat(segments.Where(segment => segment.Change == change).Select(segment => segment.Text));

    [Fact]
    public void Compare_MarksOnlyTheChangedWords()
    {
        var (left, right) = TextDiff.Compare("the quick brown fox", "the slow brown fox");

        Assert.Equal("quick", Marked(left, ComparisonChange.Removed));
        Assert.Equal("slow", Marked(right, ComparisonChange.Added));
        Assert.Equal("the quick brown fox", string.Concat(left.Select(segment => segment.Text)));
        Assert.Equal("the slow brown fox", string.Concat(right.Select(segment => segment.Text)));
    }

    [Fact]
    public void Compare_HandlesInsertionsAndDeletionsInTheMiddle()
    {
        var (left, right) = TextDiff.Compare("a b c d e", "a c x e");

        Assert.Equal("b d", Marked(left, ComparisonChange.Removed).Trim().Replace("  ", " "));
        Assert.Contains("x", Marked(right, ComparisonChange.Added), StringComparison.Ordinal);
    }

    [Fact]
    public void Compare_MarksEverythingWhenTheTextIsTooLargeToCompare()
    {
        var a = string.Join(' ', Enumerable.Range(0, 2500).Select(i => $"a{i}"));
        var b = string.Join(' ', Enumerable.Range(0, 2500).Select(i => $"b{i}"));

        var (left, right) = TextDiff.Compare(a, b);

        Assert.Equal(ComparisonChange.Removed, left.Single(segment => segment.Text.Contains("a0", StringComparison.Ordinal)).Change);
        Assert.Equal(ComparisonChange.Added, right.Single(segment => segment.Text.Contains("b0", StringComparison.Ordinal)).Change);
    }

    [Fact]
    public void Render_EncodesTextAndMarksChangesWithoutRelyingOnColour()
    {
        var html = ComparisonHtml.Render(
        [
            new ComparisonSegment("<b>x</b> "),
            new ComparisonSegment("old", ComparisonChange.Removed),
            new ComparisonSegment("new", ComparisonChange.Added)
        ]);

        var text = html.ToString();
        Assert.Contains("&lt;b&gt;x&lt;/b&gt;", text, StringComparison.Ordinal);
        Assert.Contains("<span class=\"removed\">old</span>", text, StringComparison.Ordinal);
        Assert.Contains("<span class=\"added\">new</span>", text, StringComparison.Ordinal);
    }
}
