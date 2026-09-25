namespace CDC.Web.Models;

/// <summary>
/// A saved disease ranking filter: an affected species plus the disease characteristics used
/// to narrow the disease ranking report.
/// </summary>
public sealed record DiseaseRankingFilter
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public required Guid SpeciesId { get; init; }

    public required string DiseaseType { get; init; }

    public required string ZoonoticStatus { get; init; }

    public required string NotifiableStatus { get; init; }

    public required string InfectiousStatus { get; init; }

    public IReadOnlyList<string> InfectiousAgents { get; init; } = [];
}

/// <summary>Name shown in the "Select filter" list, without loading the full filter.</summary>
public sealed record DiseaseRankingFilterSummary(Guid Id, string Name);
