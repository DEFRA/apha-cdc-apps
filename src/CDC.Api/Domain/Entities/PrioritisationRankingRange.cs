using CDC.Api.Domain.Common;

namespace CDC.Api.Domain.Entities;

/// <summary>
/// The numeric range that per-category weighted prioritisation scores are normalised into, as
/// returned by the <c>PrioritisationRankingRange</c> result set of <c>spgaPrioritisationVariables</c>
/// (filtered to <c>PrioritisationType = 'Profile'</c>).
/// </summary>
public sealed record PrioritisationRankingRange : BaseEntity
{
    /// <summary>Gets the lower bound of the range.</summary>
    public required int LowerBound { get; init; }

    /// <summary>Gets the upper bound of the range.</summary>
    public required int UpperBound { get; init; }

    /// <summary>Gets the concurrency token, checked by <c>spuPrioritisationRankingRange</c> on save.</summary>
    public required byte[] RowVersion { get; init; }
}
