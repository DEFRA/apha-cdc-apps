namespace CDC.Web.Models;

/// <summary>
/// The ranking range profile prioritisation scores are normalised into, as returned by
/// <c>GET /api/prioritisation-variables/ranking-range</c> on CDC.Api.
/// </summary>
public sealed record PrioritisationRankingRangeDto
{
    /// <summary>Gets the lower bound of the range.</summary>
    public int LowerBound { get; init; }

    /// <summary>Gets the upper bound of the range.</summary>
    public int UpperBound { get; init; }

    /// <summary>Gets the concurrency token, base64-encoded, to echo back when saving.</summary>
    public string RowVersion { get; init; } = string.Empty;
}
