namespace CDC.Api.Domain.Entities;

/// <summary>One row of a review lookup table: <c>luSectionReviewFrequency</c> (for example
/// "Every year") or <c>luReviewStatus</c> (for example "Complete").</summary>
public sealed record ProfileReviewLookupItem
{
    /// <summary>Gets the row identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the display text.</summary>
    public string Name { get; init; } = string.Empty;
}
