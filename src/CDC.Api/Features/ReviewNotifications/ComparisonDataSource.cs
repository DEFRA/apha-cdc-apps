using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Api.Features.ReferenceData.Interfaces;
using CDC.Api.Features.Species.Interfaces;
using CDC.Common.Comparison;
using CDC.Common.Contracts;

namespace CDC.Api.Features.ReviewNotifications;

/// <summary>Reads comparison data straight from the application services, without going through HTTP.</summary>
public sealed class ComparisonDataSource(
    IProfileSectionService sections,
    IReferenceDataService referenceData,
    ISpeciesService species) : IComparisonDataSource
{
    /// <inheritdoc />
    public Task<ProfileQuestionnaireMetadataDto> GetMetadataAsync(CancellationToken cancellationToken) =>
        sections.GetProfileQuestionnaireMetadataAsync(cancellationToken);

    /// <inheritdoc />
    public Task<ProfileSectionAnswersDto> GetAnswersAsync(Guid profileVersionId, Guid profileSectionId, CancellationToken cancellationToken) =>
        sections.GetProfileSectionAnswersAsync(profileVersionId, profileSectionId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> GetReferenceValuesAsync(Guid referenceTableId, CancellationToken cancellationToken) =>
        (await referenceData.GetReferenceValuesAsync(referenceTableId, cancellationToken))
            .GroupBy(value => value.Id)
            .ToDictionary(group => group.Key, group => group.First().Value);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, string>> GetSpeciesNamesAsync(CancellationToken cancellationToken) =>
        (await species.GetAllSpeciesAsync(cancellationToken))
            .GroupBy(item => item.Id)
            .ToDictionary(group => group.Key, group => group.First().Description);
}
