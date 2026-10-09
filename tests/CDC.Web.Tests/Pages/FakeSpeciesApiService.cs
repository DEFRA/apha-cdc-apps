using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Pages;

// Test double for ISpeciesApiService so page-model tests don't need a real HTTP call.
internal sealed class FakeSpeciesApiService(
    IReadOnlyList<SpeciesDto>? species = null,
    Exception? throwOnGetAllSpecies = null,
    SpeciesDetailDto? speciesDetail = null,
    Exception? throwOnGetSpeciesDetail = null,
    IReadOnlyList<SpeciesValidParentDto>? validParents = null,
    UpdateSpeciesNameParentResult? updateResult = null,
    IReadOnlyList<SpeciesAuditTrailEntryDto>? auditTrail = null,
    SpeciesMetadataDto? speciesMetadata = null,
    SpeciesAnswerDataDto? speciesAnswerData = null,
    Exception? throwOnGetSpeciesAnswerData = null,
    IReadOnlyDictionary<Guid, IReadOnlyList<ReferenceValueDto>>? referenceValuesByTable = null,
    AddSpeciesResult? addResult = null,
    UpdateSpeciesAnswerDataResult? updateAnswerDataResult = null,
    InactivateSpeciesResult? inactivateResult = null,
    DeleteSpeciesResult? deleteResult = null,
    ChangeSpeciesPositionResult? changePositionResult = null)
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
        throwOnGetSpeciesDetail is not null
            ? Task.FromException<SpeciesDetailDto?>(throwOnGetSpeciesDetail)
            : Task.FromResult(speciesDetail);

    public Task<IReadOnlyList<SpeciesValidParentDto>> GetSpeciesValidParentsAsync(Guid speciesId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_validParents);

    public Task<UpdateSpeciesNameParentResult> UpdateSpeciesNameParentAsync(
        UpdateSpeciesNameParentRequestDto request,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(updateResult ?? new UpdateSpeciesNameParentResult { Outcome = SpeciesUpdateOutcome.Success });

    public AddSpeciesRequestDto? LastAddRequest { get; private set; }

    public Task<AddSpeciesResult> AddSpeciesAsync(AddSpeciesRequestDto request, CancellationToken cancellationToken = default)
    {
        LastAddRequest = request;

        return Task.FromResult(addResult ?? new AddSpeciesResult { Outcome = SpeciesUpdateOutcome.Success, SpeciesId = Guid.NewGuid() });
    }

    public Task<IReadOnlyList<SpeciesAuditTrailEntryDto>> GetSpeciesAuditTrailAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_auditTrail);

    public Task<SpeciesMetadataDto> GetSpeciesMetadataAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(speciesMetadata ?? new SpeciesMetadataDto());

    public Task<SpeciesAnswerDataDto?> GetSpeciesAnswerDataAsync(Guid speciesId, CancellationToken cancellationToken = default) =>
        throwOnGetSpeciesAnswerData is not null
            ? Task.FromException<SpeciesAnswerDataDto?>(throwOnGetSpeciesAnswerData)
            : Task.FromResult(speciesAnswerData);

    public Task<IReadOnlyList<ReferenceValueDto>> GetReferenceValuesAsync(Guid referenceTableId, CancellationToken cancellationToken = default) =>
        Task.FromResult(
            referenceValuesByTable is not null && referenceValuesByTable.TryGetValue(referenceTableId, out var values)
                ? values
                : []);

    public List<(Guid SpeciesId, bool IsMovingUp)> ChangePositionCalls { get; } = [];

    public Task<ChangeSpeciesPositionResult> ChangeSpeciesPositionAsync(Guid speciesId, bool isMovingUp, CancellationToken cancellationToken = default)
    {
        ChangePositionCalls.Add((speciesId, isMovingUp));

        return Task.FromResult(changePositionResult ?? new ChangeSpeciesPositionResult { Outcome = SpeciesUpdateOutcome.Success });
    }

    public UpdateSpeciesAnswerDataRequestDto? LastUpdateAnswerDataRequest { get; private set; }

    public Task<UpdateSpeciesAnswerDataResult> UpdateSpeciesAnswerDataAsync(
        UpdateSpeciesAnswerDataRequestDto request,
        CancellationToken cancellationToken = default)
    {
        LastUpdateAnswerDataRequest = request;

        return Task.FromResult(updateAnswerDataResult ?? new UpdateSpeciesAnswerDataResult { Outcome = SpeciesUpdateOutcome.Success });
    }

    public (Guid SpeciesId, InactivateSpeciesRequestDto Request)? LastInactivateRequest { get; private set; }

    public Task<InactivateSpeciesResult> InactivateSpeciesAsync(
        Guid speciesId,
        InactivateSpeciesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        LastInactivateRequest = (speciesId, request);

        return Task.FromResult(inactivateResult ?? new InactivateSpeciesResult { Outcome = SpeciesUpdateOutcome.Success });
    }

    public (Guid SpeciesId, DeleteSpeciesRequestDto Request)? LastDeleteRequest { get; private set; }

    public Task<DeleteSpeciesResult> DeleteSpeciesAsync(
        Guid speciesId,
        DeleteSpeciesRequestDto request,
        CancellationToken cancellationToken = default)
    {
        LastDeleteRequest = (speciesId, request);

        return Task.FromResult(deleteResult ?? new DeleteSpeciesResult { Outcome = SpeciesUpdateOutcome.Success });
    }
}
