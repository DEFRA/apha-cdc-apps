namespace CDC.Web.Models;

/// <summary>
/// One criterion value's new score, within an update request to CDC.Api.
/// </summary>
public sealed record CriterionValueScore
{
    /// <summary>Gets the criterion value identifier.</summary>
    public Guid ValueId { get; init; }

    /// <summary>Gets the new score.</summary>
    public int Score { get; init; }
}
