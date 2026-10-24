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
    IReadOnlyDictionary<Guid, IReadOnlyList<ProfileNoteDto>>? notesByVersionAndNoteType = null,
    Exception? throwOnGetProfileNotesByVersion = null,
    ProfileVersionValidationStatusDto? validationStatus = null,
    ProfileSectionValidationResultDto? sectionValidationResult = null,
    ProfileSectionWriteResult? clearResult = null,
    Exception? throwOnClearProfileVersionSection = null,
    ProfileSectionWriteResult? saveResult = null,
    Exception? throwOnSaveProfileVersionSection = null,
    IReadOnlyList<ProfileSectionContributionDto>? contributions = null,
    IReadOnlyList<ProfileReviewCommentDto>? reviewComments = null,
    ProfileVersionAllRevisionDatesDto? allRevisionDates = null,
    Exception? throwOnGetProfileVersionSectionContributions = null,
    Exception? throwOnGetProfileSectionReviewComments = null,
    Exception? throwOnGetProfileVersionAllRevisionDates = null,
    SaveRevisionDatesResult? saveRevisionDatesResult = null,
    Exception? throwOnSaveProfileVersionRevisionDates = null,
    ReviewCommentResult? reviewCommentResult = null,
    Exception? throwOnChangeReviewComment = null,
    IReadOnlyDictionary<Guid, ProfileSectionAnswersDto>? answersByVersion = null,
    IReadOnlyList<ProfileQuestionInfoDto>? questionInfos = null,
    Exception? throwOnGetProfileQuestionInfoList = null,
    ProfileNoteChangeResult? noteChangeResult = null,
    Exception? throwOnUpdateProfileNotes = null,
    Exception? throwOnGetProfileNotesBySection = null,
    ProfileVersionValidationStatusDto? validationStatusAfterWrite = null,
    Exception? throwOnGetProfileVersionValidationStatus = null,
    Exception? throwOnValidateProfileVersionSection = null,
    ProfileSectionWriteResult? deleteAllReviewCommentsResult = null,
    Exception? throwOnDeleteAllReviewComments = null,
    Exception? throwOnGetProfileQuestionnaireMetadata = null)
    : IProfileSectionsApiService
{
    private bool sectionWritten;

    /// <summary>Gets the row version most recently passed to <c>SaveProfileVersionSectionAsync</c>.</summary>
    public byte[]? SavedLastUpdated { get; private set; }

    /// <summary>Gets the answers most recently passed to <c>SaveProfileVersionSectionAsync</c>.</summary>
    public IReadOnlyList<ProfileFieldAnswerInputDto>? SavedAnswers { get; private set; }

    /// <summary>Gets the row version most recently passed to <c>ClearProfileVersionSectionAsync</c>.</summary>
    public byte[]? ClearedLastUpdated { get; private set; }

    /// <summary>Gets the number of times <c>DeleteAllProfileReviewCommentsAsync</c> was called.</summary>
    public int DeleteAllReviewCommentsCallCount { get; private set; }

    /// <summary>Gets the number of times <c>GetProfileVersionValidationStatusAsync</c> was called.</summary>
    public int GetValidationStatusCallCount { get; private set; }

    /// <summary>Gets the number of times <c>ValidateProfileVersionSectionAsync</c> was called.</summary>
    public int ValidateProfileVersionSectionCallCount { get; private set; }

    /// <summary>Gets the number of times <c>ClearProfileVersionSectionAsync</c> was called.</summary>
    public int ClearProfileVersionSectionCallCount { get; private set; }

    /// <summary>Gets the add request most recently passed to <c>AddProfileReviewCommentAsync</c>.</summary>
    public (Guid? ParentId, string Subject, string Reference, string Text)? AddedReviewComment { get; private set; }

    /// <summary>Gets the update request most recently passed to <c>UpdateProfileReviewCommentAsync</c>.</summary>
    public (Guid CommentId, string Subject, string Reference, string Text, byte[] LastUpdated)? UpdatedReviewComment { get; private set; }

    /// <summary>Gets the comment id most recently passed to <c>DeleteProfileReviewCommentAsync</c>.</summary>
    public Guid? DeletedReviewCommentId { get; private set; }

    /// <summary>Gets the sections most recently passed to <c>SaveProfileVersionRevisionDatesAsync</c>.</summary>
    public IReadOnlyList<ProfileSectionRevisionDatesInputDto>? SavedRevisionDates { get; private set; }

    /// <summary>Gets the number of times <c>GetProfileVersionAllRevisionDatesAsync</c> was called.</summary>
    public int GetAllRevisionDatesCallCount { get; private set; }

    /// <summary>Gets the number of times <c>SaveProfileVersionSectionAsync</c> was called.</summary>
    public int SaveProfileVersionSectionCallCount { get; private set; }

    /// <summary>Gets the number of times <c>UpdateProfileNotesAsync</c> was called.</summary>
    public int UpdateProfileNotesCallCount { get; private set; }

    /// <summary>Gets the profile version most recently passed to <c>UpdateProfileNotesAsync</c>.</summary>
    public Guid? ChangedNotesProfileVersionId { get; private set; }

    /// <summary>Gets the note type most recently passed to <c>UpdateProfileNotesAsync</c>.</summary>
    public Guid? ChangedNotesNoteTypeId { get; private set; }

    /// <summary>Gets the inserts most recently passed to <c>UpdateProfileNotesAsync</c>.</summary>
    public IReadOnlyList<ProfileNoteInsert>? SavedNoteInserts { get; private set; }

    /// <summary>Gets the updates most recently passed to <c>UpdateProfileNotesAsync</c>.</summary>
    public IReadOnlyList<ProfileNoteUpdate>? SavedNoteUpdates { get; private set; }

    /// <summary>Gets the note type of every <c>UpdateProfileNotesAsync</c> call, in call order.</summary>
    public List<Guid> NoteTypesChanged { get; } = [];

    /// <summary>Gets the section most recently passed to <c>GetProfileQuestionInfoListAsync</c>.</summary>
    public Guid? RequestedQuestionInfoSectionId { get; private set; }

    public Task<ProfileQuestionnaireMetadataDto> GetProfileQuestionnaireMetadataAsync(CancellationToken cancellationToken = default) =>
        throwOnGetProfileQuestionnaireMetadata is not null
            ? Task.FromException<ProfileQuestionnaireMetadataDto>(throwOnGetProfileQuestionnaireMetadata)
            : Task.FromResult(metadata ?? new ProfileQuestionnaireMetadataDto());

    public Task<ProfileSectionAnswersDto> GetProfileSectionAnswersAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default) =>
        throwOnGetProfileSectionAnswers is not null
            ? Task.FromException<ProfileSectionAnswersDto>(throwOnGetProfileSectionAnswers)
            : Task.FromResult(
                answersByVersion is not null && answersByVersion.TryGetValue(profileVersionId, out var versionAnswers)
                    ? versionAnswers
                    : answers ?? new ProfileSectionAnswersDto());

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
        throwOnGetProfileNotesBySection is not null
            ? Task.FromException<IReadOnlyList<ProfileNoteDto>>(throwOnGetProfileNotesBySection)
            : Task.FromResult(
                notesByNoteType is not null && notesByNoteType.TryGetValue(noteTypeId, out var notes)
                    ? notes
                    : []);

    public Task<IReadOnlyList<ProfileNoteDto>> GetProfileNotesByVersionAsync(
        Guid profileVersionId,
        Guid noteTypeId,
        CancellationToken cancellationToken = default) =>
        throwOnGetProfileNotesByVersion is not null
            ? Task.FromException<IReadOnlyList<ProfileNoteDto>>(throwOnGetProfileNotesByVersion)
            : Task.FromResult(
                notesByVersionAndNoteType is not null && notesByVersionAndNoteType.TryGetValue(noteTypeId, out var versionNotes)
                    ? versionNotes
                    : []);

    public Task<ProfileNoteChangeResult> UpdateProfileNotesAsync(
        Guid profileVersionId,
        Guid noteTypeId,
        IReadOnlyList<ProfileNoteInsert> inserts,
        IReadOnlyList<ProfileNoteUpdate> updates,
        CancellationToken cancellationToken = default)
    {
        UpdateProfileNotesCallCount++;
        ChangedNotesProfileVersionId = profileVersionId;
        ChangedNotesNoteTypeId = noteTypeId;
        NoteTypesChanged.Add(noteTypeId);
        SavedNoteInserts = inserts;
        SavedNoteUpdates = updates;

        return throwOnUpdateProfileNotes is not null
            ? Task.FromException<ProfileNoteChangeResult>(throwOnUpdateProfileNotes)
            : Task.FromResult(noteChangeResult ?? new ProfileNoteChangeResult { Outcome = ProfileNoteChangeOutcome.Success });
    }

    public Task<IReadOnlyList<ProfileQuestionInfoDto>> GetProfileQuestionInfoListAsync(
        Guid profileSectionId,
        CancellationToken cancellationToken = default)
    {
        RequestedQuestionInfoSectionId = profileSectionId;

        return throwOnGetProfileQuestionInfoList is not null
            ? Task.FromException<IReadOnlyList<ProfileQuestionInfoDto>>(throwOnGetProfileQuestionInfoList)
            : Task.FromResult(questionInfos ?? []);
    }

    public Task<ProfileVersionValidationStatusDto> GetProfileVersionValidationStatusAsync(
        Guid profileVersionId,
        CancellationToken cancellationToken = default)
    {
        GetValidationStatusCallCount++;

        if (throwOnGetProfileVersionValidationStatus is not null)
        {
            return Task.FromException<ProfileVersionValidationStatusDto>(throwOnGetProfileVersionValidationStatus);
        }

        var status = sectionWritten && validationStatusAfterWrite is not null ? validationStatusAfterWrite : validationStatus;

        return Task.FromResult(status ?? new ProfileVersionValidationStatusDto { ProfileVersionId = profileVersionId });
    }

    public Task<ProfileSectionValidationResultDto> ValidateProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default)
    {
        ValidateProfileVersionSectionCallCount++;

        return throwOnValidateProfileVersionSection is not null
            ? Task.FromException<ProfileSectionValidationResultDto>(throwOnValidateProfileVersionSection)
            : Task.FromResult(sectionValidationResult ?? new ProfileSectionValidationResultDto { IsValid = true });
    }

    public Task<ProfileSectionWriteResult> ClearProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        byte[]? lastUpdated,
        CancellationToken cancellationToken = default)
    {
        ClearProfileVersionSectionCallCount++;
        ClearedLastUpdated = lastUpdated;

        if (throwOnClearProfileVersionSection is not null)
        {
            return Task.FromException<ProfileSectionWriteResult>(throwOnClearProfileVersionSection);
        }

        var result = clearResult ?? new ProfileSectionWriteResult { Outcome = ProfileSectionWriteOutcome.Success };
        sectionWritten |= result.Outcome == ProfileSectionWriteOutcome.Success;

        return Task.FromResult(result);
    }

    public Task<ProfileSectionWriteResult> SaveProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        IReadOnlyList<ProfileFieldAnswerInputDto> answers,
        byte[]? lastUpdated,
        CancellationToken cancellationToken = default)
    {
        SaveProfileVersionSectionCallCount++;
        SavedAnswers = answers;
        SavedLastUpdated = lastUpdated;

        if (throwOnSaveProfileVersionSection is not null)
        {
            return Task.FromException<ProfileSectionWriteResult>(throwOnSaveProfileVersionSection);
        }

        var result = saveResult ?? new ProfileSectionWriteResult { Outcome = ProfileSectionWriteOutcome.Success };
        sectionWritten |= result.Outcome == ProfileSectionWriteOutcome.Success;

        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<ProfileSectionContributionDto>> GetProfileVersionSectionContributionsAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default) =>
        throwOnGetProfileVersionSectionContributions is not null
            ? Task.FromException<IReadOnlyList<ProfileSectionContributionDto>>(throwOnGetProfileVersionSectionContributions)
            : Task.FromResult(contributions ?? []);

    public Task<IReadOnlyList<ProfileReviewCommentDto>> GetProfileSectionReviewCommentsAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default) =>
        throwOnGetProfileSectionReviewComments is not null
            ? Task.FromException<IReadOnlyList<ProfileReviewCommentDto>>(throwOnGetProfileSectionReviewComments)
            : Task.FromResult(reviewComments ?? []);

    public Task<ReviewCommentResult> AddProfileReviewCommentAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid? parentId,
        string subject,
        string commentReference,
        string commentText,
        CancellationToken cancellationToken = default)
    {
        AddedReviewComment = (parentId, subject, commentReference, commentText);

        return ReviewCommentResultTask();
    }

    public Task<ReviewCommentResult> UpdateProfileReviewCommentAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid commentId,
        ReviewCommentContent content,
        byte[] lastUpdated,
        CancellationToken cancellationToken = default)
    {
        UpdatedReviewComment = (commentId, content.Subject, content.CommentReference, content.CommentText, lastUpdated);

        return ReviewCommentResultTask();
    }

    public Task<ReviewCommentResult> DeleteProfileReviewCommentAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        DeletedReviewCommentId = commentId;

        return ReviewCommentResultTask();
    }

    private Task<ReviewCommentResult> ReviewCommentResultTask() =>
        throwOnChangeReviewComment is not null
            ? Task.FromException<ReviewCommentResult>(throwOnChangeReviewComment)
            : Task.FromResult(reviewCommentResult ?? new ReviewCommentResult { Outcome = ReviewCommentOutcome.Success });

    public Task<ProfileSectionWriteResult> DeleteAllProfileReviewCommentsAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default)
    {
        DeleteAllReviewCommentsCallCount++;

        return throwOnDeleteAllReviewComments is not null
            ? Task.FromException<ProfileSectionWriteResult>(throwOnDeleteAllReviewComments)
            : Task.FromResult(deleteAllReviewCommentsResult ?? new ProfileSectionWriteResult { Outcome = ProfileSectionWriteOutcome.Success });
    }

    public Task<ProfileVersionAllRevisionDatesDto> GetProfileVersionAllRevisionDatesAsync(
        Guid profileVersionId,
        CancellationToken cancellationToken = default)
    {
        GetAllRevisionDatesCallCount++;

        return throwOnGetProfileVersionAllRevisionDates is not null
            ? Task.FromException<ProfileVersionAllRevisionDatesDto>(throwOnGetProfileVersionAllRevisionDates)
            : Task.FromResult(allRevisionDates ?? new ProfileVersionAllRevisionDatesDto { ProfileVersionId = profileVersionId });
    }

    public Task<SaveRevisionDatesResult> SaveProfileVersionRevisionDatesAsync(
        Guid profileVersionId,
        IReadOnlyList<ProfileSectionRevisionDatesInputDto> sections,
        CancellationToken cancellationToken = default)
    {
        SavedRevisionDates = sections;

        return throwOnSaveProfileVersionRevisionDates is not null
            ? Task.FromException<SaveRevisionDatesResult>(throwOnSaveProfileVersionRevisionDates)
            : Task.FromResult(saveRevisionDatesResult ?? new SaveRevisionDatesResult { Outcome = SaveRevisionDatesOutcome.Success, SectionsSavedCount = sections.Count });
    }
}
