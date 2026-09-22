using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Pages;

// Test double for ISpeciesApiService so page-model tests don't need a real HTTP call.
internal sealed class FakeSpeciesApiService(IReadOnlyList<SpeciesDto>? species = null, Exception? throwOnGetAllSpecies = null)
    : ISpeciesApiService
{
    private readonly IReadOnlyList<SpeciesDto> _species = species ?? [];

    public Task<IReadOnlyList<SpeciesDto>> GetAllSpeciesAsync(CancellationToken cancellationToken = default) =>
        throwOnGetAllSpecies is not null
            ? Task.FromException<IReadOnlyList<SpeciesDto>>(throwOnGetAllSpecies)
            : Task.FromResult(_species);
}
