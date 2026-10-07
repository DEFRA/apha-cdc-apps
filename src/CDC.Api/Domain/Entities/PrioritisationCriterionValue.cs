using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// One scoreable value of a <see cref="PrioritisationCriterion"/> (e.g. "N/A", "10", "100s"), as
/// returned by the <c>PrioritisationCriterionValue</c> result set of
/// <c>spgaPrioritisationVariables</c>.
/// </summary>
public sealed record PrioritisationCriterionValue : BaseEntity
{
    /// <summary>Gets the identifier of the owning criterion.</summary>
    public required Guid CriterionId { get; init; }

    /// <summary>Gets the display label for this value, e.g. "100s".</summary>
    public required string Value { get; init; }

    /// <summary>Gets the score assigned to this value.</summary>
    public required int Score { get; init; }
}
