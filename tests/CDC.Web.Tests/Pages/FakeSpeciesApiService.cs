using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Pages;

// Test double for ISpeciesApiService so page-model tests don't need a real HTTP call.
internal sealed class FakeSpeciesApiService(
    IReadOnlyList<SpeciesDto>? species = null,
    Exception? throwOnGetAllSpecies = null,
    SpeciesDetailDto? speciesDetail = null,
    IReadOnlyList<SpeciesValidParentDto>? validParents = null,
    UpdateSpeciesNameParentResult? updateResult = null,
    IReadOnlyList<SpeciesAuditTrailEntryDto>? auditTrail = null)
    : ISpeciesApiService
{
    private readonly IReadOnlyList<SpeciesDto> _species = species ?? [];
    private readonly IReadOnlyList<SpeciesValidParentDto> _validParents = validParents ?? [];
    private readonly IReadOnlyList<SpeciesAuditTrailEntryDto> _auditTrail = auditTrail ?? [];

    public Task<IReadOnlyList<SpeciesDto>> GetAllSpeciesAsync(CancellationToken cancellationToken = default) =>
        throwOnGetAllSpecies is not null
            ? Task.FromException<IReadOnlyList<SpeciesDto>>(throwOnGetAllSpecies)
            : Task.FromResult(_species);

    public Task<SpeciesDetailDto?> GetSpeciesDetailAsync(Guid speciesId, CancellationToken cancellationToken = default) =>
        Task.FromResult(speciesDetail);

    public Task<IReadOnlyList<SpeciesValidParentDto>> GetSpeciesValidParentsAsync(Guid speciesId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_validParents);

    public Task<UpdateSpeciesNameParentResult> UpdateSpeciesNameParentAsync(
        UpdateSpeciesNameParentRequestDto request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(updateResult ?? new UpdateSpeciesNameParentResult { Outcome = SpeciesUpdateOutcome.Success });

    public Task<IReadOnlyList<SpeciesAuditTrailEntryDto>> GetSpeciesAuditTrailAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_auditTrail);
}
