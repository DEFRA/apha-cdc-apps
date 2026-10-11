using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

// Test double for IProfileSectionsApiService so page-model tests don't need a real HTTP call.
internal sealed class FakeProfileSectionsApiService(
    ProfileQuestionnaireMetadataDto? metadata = null,
    ProfileSectionAnswersDto? answers = null,
    Exception? throwOnGetProfileSectionAnswers = null,
    IReadOnlyDictionary<Guid, IReadOnlyList<ReferenceValueDto>>? referenceValuesByTable = null,
    IReadOnlyList<ProfileNoteTypeDto>? noteTypes = null,
    IReadOnlyDictionary<Guid, IReadOnlyList<ProfileNoteDto>>? notesByNoteType = null,
    Exception? throwOnGetProfileNoteTypes = null,
    ProfileVersionValidationStatusDto? validationStatus = null,
    ProfileSectionValidationResultDto? sectionValidationResult = null,
    ClearProfileSectionResultDto? clearResult = null,
    Exception? throwOnClearProfileVersionSection = null,
    SaveProfileSectionResultDto? saveResult = null,
    Exception? throwOnSaveProfileVersionSection = null,
    IReadOnlyList<ProfileSectionContributionDto>? contributions = null,
    IReadOnlyList<ProfileReviewCommentDto>? reviewComments = null,
    ProfileVersionAllRevisionDatesDto? allRevisionDates = null)
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

    public Task<IReadOnlyList<ProfileNoteTypeDto>> GetProfileNoteTypesAsync(CancellationToken cancellationToken = default) =>
        throwOnGetProfileNoteTypes is not null
            ? Task.FromException<IReadOnlyList<ProfileNoteTypeDto>>(throwOnGetProfileNoteTypes)
            : Task.FromResult(noteTypes ?? []);

    public Task<IReadOnlyList<ProfileNoteDto>> GetProfileNotesBySectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid noteTypeId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(
            notesByNoteType is not null && notesByNoteType.TryGetValue(noteTypeId, out var notes)
                ? notes
                : []);

    public Task<ProfileVersionValidationStatusDto> GetProfileVersionValidationStatusAsync(
        Guid profileVersionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(validationStatus ?? new ProfileVersionValidationStatusDto { ProfileVersionId = profileVersionId });

    public Task<ProfileSectionValidationResultDto> ValidateProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(sectionValidationResult ?? new ProfileSectionValidationResultDto { IsValid = true });

    public Task<ClearProfileSectionResultDto> ClearProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default) =>
        throwOnClearProfileVersionSection is not null
            ? Task.FromException<ClearProfileSectionResultDto>(throwOnClearProfileVersionSection)
            : Task.FromResult(clearResult ?? new ClearProfileSectionResultDto());

    public Task<SaveProfileSectionResultDto> SaveProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        IReadOnlyList<ProfileFieldAnswerInputDto> answers,
        CancellationToken cancellationToken = default) =>
        throwOnSaveProfileVersionSection is not null
            ? Task.FromException<SaveProfileSectionResultDto>(throwOnSaveProfileVersionSection)
            : Task.FromResult(saveResult ?? new SaveProfileSectionResultDto());

    public Task<IReadOnlyList<ProfileSectionContributionDto>> GetProfileVersionSectionContributionsAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(contributions ?? []);

    public Task<IReadOnlyList<ProfileReviewCommentDto>> GetProfileSectionReviewCommentsAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(reviewComments ?? []);

    public Task<ProfileVersionAllRevisionDatesDto> GetProfileVersionAllRevisionDatesAsync(
        Guid profileVersionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(allRevisionDates ?? new ProfileVersionAllRevisionDatesDto { ProfileVersionId = profileVersionId });
}
