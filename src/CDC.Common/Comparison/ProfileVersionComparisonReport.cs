namespace CDC.Common.Comparison;

/// <summary>The differences between two profile versions, ready to render as a PDF.</summary>
public sealed record ProfileVersionComparisonReport
{
    /// <inheritdoc/>
    public required string LeftTitle { get; init; }

    /// <inheritdoc/>
    public required string RightTitle { get; init; }

    /// <inheritdoc/>
    public required IReadOnlyList<ComparisonSection> Sections { get; init; }
}

/// <summary>One profile section; <see cref="Questions"/> holds only the questions that differ.</summary>
public sealed record ComparisonSection(string Heading, IReadOnlyList<ComparisonQuestion> Questions)
{
    /// <inheritdoc/>
    public bool IsIdentical => Questions.Count == 0;
}

/// <summary>A question with at least one differing answer.</summary>
public sealed record ComparisonQuestion(string Number, string Text, IReadOnlyList<ComparisonGroup> Groups);

/// <summary>
/// The differing fields for one part of a question: the whole question when it is standard, one
/// species when it is answered per species, or one row when it repeats. <see cref="Heading"/> is
/// <see langword="null"/> for a standard question.
/// </summary>
public sealed record ComparisonGroup(string? Heading, IReadOnlyList<ComparisonField> Fields);

/// <summary>One differing field. Left holds unchanged and removed text, right unchanged and added text.</summary>
public sealed record ComparisonField(string Label, IReadOnlyList<ComparisonSegment> Left, IReadOnlyList<ComparisonSegment> Right);

/// <summary>A run of text and how it changed.</summary>
public sealed record ComparisonSegment(string Text, ComparisonChange Change = ComparisonChange.None);

/// <summary>How a run of text differs between the two versions.</summary>
public enum ComparisonChange
{
    /// <summary>The same in both versions, or a placeholder.</summary>
    None,

    /// <summary>Only in the earlier version.</summary>
    Removed,

    /// <summary>Only in the later version.</summary>
    Added
}
