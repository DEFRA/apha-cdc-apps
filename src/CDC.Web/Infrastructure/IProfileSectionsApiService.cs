using CDC.Common.Contracts;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Typed client for the profile sections endpoints on CDC.Api.
/// </summary>
public interface IProfileSectionsApiService
{
    /// <summary>Calls <c>GET /api/profile-sections/metadata</c>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The 16 profile reference sections, their questions and fields.</returns>
    Task<ProfileQuestionnaireMetadataDto> GetProfileQuestionnaireMetadataAsync(CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/profile-sections/answers</c>.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The section's question names and recorded field values.</returns>
    Task<ProfileSectionAnswersDto> GetProfileSectionAnswersAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/reference-data/{referenceTableId}/values</c>, used to resolve
    /// "List"/"MultiValueList" type question field options.</summary>
    /// <param name="referenceTableId">The reference table to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The reference values; an empty list when the table has none.</returns>
    Task<IReadOnlyList<ReferenceValueDto>> GetReferenceValuesAsync(Guid referenceTableId, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/profile-notes/types</c>, used to resolve the note type ids for
    /// the References and Further information tabs.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every profile note type.</returns>
    Task<IReadOnlyList<ProfileNoteTypeDto>> GetProfileNoteTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/profile-notes/by-section</c>.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="noteTypeId">The note type to filter by.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The matching notes; an empty list when none have been recorded.</returns>
    Task<IReadOnlyList<ProfileNoteDto>> GetProfileNotesBySectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid noteTypeId,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/profile-notes/by-version</c>, used by the standalone Notes page
    /// to list every note of a given type across the whole profile version, not just one section.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="noteTypeId">The note type to filter by.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The matching notes; an empty list when none have been recorded.</returns>
    Task<IReadOnlyList<ProfileNoteDto>> GetProfileNotesByVersionAsync(
        Guid profileVersionId,
        Guid noteTypeId,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/profile-sections/validation-status</c>.</summary>
    /// <param name="profileVersionId">The profile version to check.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The profile version's per-section validation status.</returns>
    Task<ProfileVersionValidationStatusDto> GetProfileVersionValidationStatusAsync(
        Guid profileVersionId,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/profile-sections/validate</c>.</summary>
    /// <param name="profileVersionId">The profile version to validate.</param>
    /// <param name="profileSectionId">The section to validate.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the section is valid, and which mandatory questions are unanswered.</returns>
    Task<ProfileSectionValidationResultDto> ValidateProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>POST /api/profile-sections/clear</c>.</summary>
    /// <param name="profileVersionId">The profile version to clear.</param>
    /// <param name="profileSectionId">The section to clear.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The number of fields cleared.</returns>
    Task<ClearProfileSectionResultDto> ClearProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>POST /api/profile-sections/save</c>.</summary>
    /// <param name="profileVersionId">The profile version being answered.</param>
    /// <param name="profileSectionId">The section being answered.</param>
    /// <param name="answers">The submitted field answers.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The number of fields saved.</returns>
    Task<SaveProfileSectionResultDto> SaveProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        IReadOnlyList<ProfileFieldAnswerInputDto> answers,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/profile-sections/contributions</c>.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every logged contribution, most recent first.</returns>
    Task<IReadOnlyList<ProfileSectionContributionDto>> GetProfileVersionSectionContributionsAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/profile-sections/review-comments</c>.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every review comment, in thread order.</returns>
    Task<IReadOnlyList<ProfileReviewCommentDto>> GetProfileSectionReviewCommentsAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>POST /api/profile-sections/review-comments</c>.</summary>
    /// <param name="profileVersionId">The profile version being commented on.</param>
    /// <param name="profileSectionId">The section being commented on.</param>
    /// <param name="parentId">The comment being replied to, or <see langword="null"/> for a top-level comment.</param>
    /// <param name="subject">The subject line.</param>
    /// <param name="commentReference">The reference text.</param>
    /// <param name="commentText">The comment body.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome of the add.</returns>
    Task<ReviewCommentResult> AddProfileReviewCommentAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid? parentId,
        string subject,
        string commentReference,
        string commentText,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>PUT /api/profile-sections/review-comments</c>.</summary>
    /// <param name="profileVersionId">The profile version the comment belongs to.</param>
    /// <param name="profileSectionId">The section the comment belongs to.</param>
    /// <param name="commentId">The comment being edited.</param>
    /// <param name="subject">The new subject line.</param>
    /// <param name="commentReference">The new reference text.</param>
    /// <param name="commentText">The new comment body.</param>
    /// <param name="lastUpdated">The comment's row version as last read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome of the edit.</returns>
    Task<ReviewCommentResult> UpdateProfileReviewCommentAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid commentId,
        string subject,
        string commentReference,
        string commentText,
        byte[] lastUpdated,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>DELETE /api/profile-sections/review-comments/{id}</c>.</summary>
    /// <param name="profileVersionId">The profile version the comment belongs to.</param>
    /// <param name="profileSectionId">The section the comment belongs to.</param>
    /// <param name="commentId">The comment to delete.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome of the delete.</returns>
    Task<ReviewCommentResult> DeleteProfileReviewCommentAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid commentId,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/profile-sections/all-revision-dates</c>.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Each section's revision data (in section-number order) and the drop-down options.</returns>
    Task<ProfileVersionAllRevisionDatesDto> GetProfileVersionAllRevisionDatesAsync(
        Guid profileVersionId,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>PUT /api/profile-sections/all-revision-dates</c>.</summary>
    /// <param name="profileVersionId">The profile version being edited.</param>
    /// <param name="sections">The submitted revision data of each section.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome, distinguishing a concurrent-edit conflict and a rejected submission from success.</returns>
    Task<SaveRevisionDatesResult> SaveProfileVersionRevisionDatesAsync(
        Guid profileVersionId,
        IReadOnlyList<ProfileSectionRevisionDatesInputDto> sections,
        CancellationToken cancellationToken = default);
}
