namespace CDC.Web.Models;

/// <summary>
/// A prioritisation criterion within a category, as returned by
/// <c>GET /api/prioritisation-variables/categories</c> on CDC.Api.
/// </summary>
public sealed record PrioritisationCriterionDto
{
    /// <summary>Gets the criterion identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the short code shown alongside the criterion name.</summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>Gets the criterion name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the weighting applied to this criterion's scores.</summary>
    public int Weight { get; init; }

    /// <summary>Gets the scoreable values for this criterion, in sequence order.</summary>
    public IReadOnlyList<PrioritisationCriterionValueDto> Values { get; init; } = [];
}
