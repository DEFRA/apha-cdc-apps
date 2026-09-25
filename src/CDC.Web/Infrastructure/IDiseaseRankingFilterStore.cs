using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Stores disease ranking filters created on the "Create and maintain disease ranking filters"
/// page.
/// </summary>
/// <remarks>
/// CDC.Api has no disease ranking filter endpoints yet, so this is an in-memory placeholder
/// (state lives for the lifetime of the container, not the database) that lets the GDS-compliant
/// UI be built and exercised end-to-end now. Replace with a typed <c>HttpClient</c> service
/// calling CDC.Api once the corresponding feature exists there - see
/// <see cref="ISpeciesApiService"/> for the pattern to follow.
/// </remarks>
public interface IDiseaseRankingFilterStore
{
    /// <summary>Every saved filter, ordered by name.</summary>
    IReadOnlyList<DiseaseRankingFilterSummary> GetAll();

    /// <summary>Attempts to find a saved filter by id.</summary>
    bool TryGet(Guid id, out DiseaseRankingFilter filter);

    /// <summary>Creates or updates a filter, keyed by <see cref="DiseaseRankingFilter.Id"/>.</summary>
    void Save(DiseaseRankingFilter filter);

    /// <summary>Deletes a filter. Does nothing if it does not exist.</summary>
    void Delete(Guid id);
}
