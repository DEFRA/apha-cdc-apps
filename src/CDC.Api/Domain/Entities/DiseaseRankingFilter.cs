namespace CDC.Api.Domain.Entities;

/// <summary>
/// One disease ranking filter, summarised for the "Select Filter" dropdown. Backed by the
/// legacy <c>DiseaseFilters</c> table, which stores one row per selected species per filter;
/// <see cref="FilterNumber"/> groups those rows into a single filter.
/// </summary>
public sealed class DiseaseRankingFilterSummary
{
    /// <summary>Gets the filter's identifying number.</summary>
    public required long FilterNumber { get; init; }

    /// <summary>Gets the filter's display name.</summary>
    public required string DiseaseName { get; init; }
}

/// <summary>
/// Full detail for one disease ranking filter: its name, criteria and selected species.
/// </summary>
public sealed class DiseaseRankingFilterDetail
{
    /// <summary>Gets the filter's identifying number.</summary>
    public required long FilterNumber { get; init; }

    /// <summary>Gets the filter's display name.</summary>
    public required string DiseaseName { get; init; }

    /// <summary>Gets the endemic/exotic/both criterion.</summary>
    public required string Disease1 { get; init; }

    /// <summary>Gets the zoonotic/non-zoonotic/both criterion.</summary>
    public required string Disease2 { get; init; }

    /// <summary>Gets the notifiable/non-notifiable/both criterion.</summary>
    public required string Disease3 { get; init; }

    /// <summary>Gets the infectious/non-infectious/both criterion.</summary>
    public required string Disease4 { get; init; }

    /// <summary>Gets the selected transmissible agent types (bacteria, virus, fungus, parasite, other).</summary>
    public required IReadOnlyList<string> DiseaseTypes { get; init; }

    /// <summary>Gets every species selected for this filter.</summary>
    public required IReadOnlyList<Guid> SpeciesIds { get; init; }
}
