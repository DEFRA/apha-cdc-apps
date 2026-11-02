using System.Text.RegularExpressions;

namespace CDC.Common.Comparison;

/// <summary>Word-level text comparison, standing in for the legacy diff-match-patch highlighting.</summary>
public static partial class TextDiff
{
    // Beyond this many word pairs the table is too large to build; the whole text is marked instead.
    private const long MaxCells = 4_000_000;

    [GeneratedRegex(@"\s+|[^\s]+", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex TokenPattern();

    /// <summary>Compares two texts.</summary>
    /// <param name="left">The earlier text.</param>
    /// <param name="right">The later text.</param>
    /// <returns>The left text split into unchanged and removed runs, and the right into unchanged and added runs.</returns>
    public static (IReadOnlyList<ComparisonSegment> Left, IReadOnlyList<ComparisonSegment> Right) Compare(string left, string right)
    {
        var a = Tokenise(left);
        var b = Tokenise(right);

        var start = 0;
        while (start < a.Length && start < b.Length && a[start] == b[start])
        {
            start++;
        }

        var endA = a.Length;
        var endB = b.Length;
        while (endA > start && endB > start && a[endA - 1] == b[endB - 1])
        {
            endA--;
            endB--;
        }

        var leftOps = new List<(string, ComparisonChange)>();
        var rightOps = new List<(string, ComparisonChange)>();

        leftOps.AddRange(a[..start].Select(token => (token, ComparisonChange.None)));
        rightOps.AddRange(b[..start].Select(token => (token, ComparisonChange.None)));

        DiffMiddle(a[start..endA], b[start..endB], leftOps, rightOps);

        leftOps.AddRange(a[endA..].Select(token => (token, ComparisonChange.None)));
        rightOps.AddRange(b[endB..].Select(token => (token, ComparisonChange.None)));

        return (Merge(leftOps), Merge(rightOps));
    }

    private static string[] Tokenise(string text) =>
        [.. TokenPattern().Matches(text).Select(match => match.Value)];

    private static void DiffMiddle(string[] a, string[] b, List<(string, ComparisonChange)> leftOps, List<(string, ComparisonChange)> rightOps)
    {
        if (a.Length == 0 || b.Length == 0 || (long)a.Length * b.Length > MaxCells)
        {
            leftOps.AddRange(a.Select(token => (token, ComparisonChange.Removed)));
            rightOps.AddRange(b.Select(token => (token, ComparisonChange.Added)));

            return;
        }

        var lengths = new int[a.Length + 1, b.Length + 1];

        for (var i = a.Length - 1; i >= 0; i--)
        {
            for (var j = b.Length - 1; j >= 0; j--)
            {
                lengths[i, j] = a[i] == b[j] ? lengths[i + 1, j + 1] + 1 : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }

        int x = 0, y = 0;
        while (x < a.Length && y < b.Length)
        {
            if (a[x] == b[y])
            {
                leftOps.Add((a[x++], ComparisonChange.None));
                rightOps.Add((b[y++], ComparisonChange.None));
            }
            else if (lengths[x + 1, y] >= lengths[x, y + 1])
            {
                leftOps.Add((a[x++], ComparisonChange.Removed));
            }
            else
            {
                rightOps.Add((b[y++], ComparisonChange.Added));
            }
        }

        leftOps.AddRange(a[x..].Select(token => (token, ComparisonChange.Removed)));
        rightOps.AddRange(b[y..].Select(token => (token, ComparisonChange.Added)));
    }

    // Adjacent tokens with the same change become one run, so spaces between two changed words are marked too.
    private static List<ComparisonSegment> Merge(List<(string Text, ComparisonChange Change)> ops)
    {
        var segments = new List<ComparisonSegment>();

        foreach (var (text, change) in ops)
        {
            if (segments.Count > 0 && segments[^1].Change == change)
            {
                segments[^1] = segments[^1] with { Text = segments[^1].Text + text };
            }
            else
            {
                segments.Add(new ComparisonSegment(text, change));
            }
        }

        return segments;
    }
}
