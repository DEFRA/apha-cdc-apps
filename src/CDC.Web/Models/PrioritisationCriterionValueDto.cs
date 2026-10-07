namespace CDC.Web.Models;

/// <summary>
/// One scoreable value of a prioritisation criterion (e.g. "N/A", "10", "100s"), as returned by
/// <c>GET /api/prioritisation-variables/categories</c> on CDC.Api.
/// </summary>
public sealed record PrioritisationCriterionValueDto
{
    /// <summary>Gets the criterion value identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the display label for this value.</summary>
    public string Value { get; init; } = string.Empty;

    /// <summary>Gets the score assigned to this value.</summary>
    public int Score { get; init; }
}
