using System.Collections.Concurrent;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <inheritdoc cref="IDiseaseRankingFilterStore" />
public sealed class InMemoryDiseaseRankingFilterStore : IDiseaseRankingFilterStore
{
    private readonly ConcurrentDictionary<Guid, DiseaseRankingFilter> _filters = new();

    /// <inheritdoc />
    public IReadOnlyList<DiseaseRankingFilterSummary> GetAll() =>
        [.. _filters.Values
            .OrderBy(filter => filter.Name, StringComparer.OrdinalIgnoreCase)
            .Select(filter => new DiseaseRankingFilterSummary(filter.Id, filter.Name))];

    /// <inheritdoc />
    public bool TryGet(Guid id, out DiseaseRankingFilter filter) => _filters.TryGetValue(id, out filter!);

    /// <inheritdoc />
    public void Save(DiseaseRankingFilter filter) => _filters[filter.Id] = filter;

    /// <inheritdoc />
    public void Delete(Guid id) => _filters.TryRemove(id, out _);
}
