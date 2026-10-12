using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Pages;

// Test double for IDiseaseRankingFiltersApiService; captures the last save request so tests can
// assert exactly what the page model sent, independent of model binding correctness.
internal sealed class FakeDiseaseRankingFiltersApiService(
    IReadOnlyList<DiseaseRankingFilterSummaryDto>? filters = null,
    DiseaseRankingFilterDetailDto? filterDetail = null,
    SaveDiseaseRankingFilterResult? saveResult = null)
    : IDiseaseRankingFiltersApiService
{
    private readonly IReadOnlyList<DiseaseRankingFilterSummaryDto> _filters = filters ?? [];

    public SaveDiseaseRankingFilterRequestDto? LastCreateRequest { get; private set; }

    public (long FilterNumber, SaveDiseaseRankingFilterRequestDto Request)? LastUpdateRequest { get; private set; }

    public Task<IReadOnlyList<DiseaseRankingFilterSummaryDto>> GetFiltersAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_filters);

    public Task<DiseaseRankingFilterDetailDto?> GetFilterDetailAsync(long filterNumber, CancellationToken cancellationToken = default) =>
        Task.FromResult(filterDetail);

    public Task<SaveDiseaseRankingFilterResult> CreateFilterAsync(SaveDiseaseRankingFilterRequestDto request, CancellationToken cancellationToken = default)
    {
        LastCreateRequest = request;

        return Task.FromResult(saveResult ?? new SaveDiseaseRankingFilterResult { Outcome = DiseaseRankingFilterSaveOutcome.Success, FilterNumber = 1 });
    }

    public Task<SaveDiseaseRankingFilterResult> UpdateFilterAsync(
        long filterNumber,
        SaveDiseaseRankingFilterRequestDto request,
        CancellationToken cancellationToken = default)
    {
        LastUpdateRequest = (filterNumber, request);

        return Task.FromResult(saveResult ?? new SaveDiseaseRankingFilterResult { Outcome = DiseaseRankingFilterSaveOutcome.Success, FilterNumber = filterNumber });
    }

    public Task DeleteFilterAsync(long filterNumber, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
