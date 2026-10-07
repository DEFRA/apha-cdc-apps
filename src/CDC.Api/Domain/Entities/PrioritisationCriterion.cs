using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// A prioritisation criterion belonging to one <see cref="PrioritisationCategory"/>, as returned
/// by the <c>PrioritisationCriterion</c> result set of <c>spgaPrioritisationVariables</c>.
/// </summary>
public sealed record PrioritisationCriterion : BaseEntity
{
    /// <summary>Gets the identifier of the owning category.</summary>
    public required Guid CategoryId { get; init; }

    /// <summary>Gets the short code shown alongside the criterion name, e.g. "C1".</summary>
    public required string Code { get; init; }

    /// <summary>Gets the criterion name.</summary>
    public required string Name { get; init; }

    /// <summary>Gets the weighting applied to this criterion's scores.</summary>
    public required int Weight { get; init; }

    /// <summary>Gets the scoreable values for this criterion, in sequence order.</summary>
    public required IReadOnlyList<PrioritisationCriterionValue> Values { get; init; }
}
