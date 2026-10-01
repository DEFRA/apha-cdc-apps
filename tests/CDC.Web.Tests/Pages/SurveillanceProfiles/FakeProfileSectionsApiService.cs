using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

// Test double for IProfileSectionsApiService so page-model tests don't need a real HTTP call.
internal sealed class FakeProfileSectionsApiService(
    ProfileQuestionnaireMetadataDto? metadata = null,
    ProfileSectionAnswersDto? answers = null,
    Exception? throwOnGetProfileSectionAnswers = null,
    IReadOnlyDictionary<Guid, IReadOnlyList<ReferenceValueDto>>? referenceValuesByTable = null)
    : IProfileSectionsApiService
{
    public Task<ProfileQuestionnaireMetadataDto> GetProfileQuestionnaireMetadataAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(metadata ?? new ProfileQuestionnaireMetadataDto());

    public Task<ProfileSectionAnswersDto> GetProfileSectionAnswersAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default) =>
        throwOnGetProfileSectionAnswers is not null
            ? Task.FromException<ProfileSectionAnswersDto>(throwOnGetProfileSectionAnswers)
            : Task.FromResult(answers ?? new ProfileSectionAnswersDto());

    public Task<IReadOnlyList<ReferenceValueDto>> GetReferenceValuesAsync(Guid referenceTableId, CancellationToken cancellationToken = default) =>
        Task.FromResult(
            referenceValuesByTable is not null && referenceValuesByTable.TryGetValue(referenceTableId, out var values)
                ? values
                : []);
}
