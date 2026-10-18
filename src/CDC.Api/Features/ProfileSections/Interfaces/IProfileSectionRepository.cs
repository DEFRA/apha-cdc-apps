using CDC.Api.Domain.Entities;

namespace CDC.Api.Features.ProfileSections.Interfaces;

/// <summary>
/// Data access for the profile reference sections, their questionnaire metadata, and the
/// recorded answers for a profile version. Implementations must contain no business logic.
/// </summary>
public interface IProfileSectionRepository
{
    /// <summary>Reads the questionnaire structure via <c>spgaProfileSectionMetadata</c>.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The 16 profile reference sections, their questions and fields.</returns>
    Task<ProfileQuestionnaireMetadata> GetProfileQuestionnaireMetadataAsync(CancellationToken cancellationToken);

    /// <summary>Reads one profile version's recorded answers for one section via <c>spgProfileVersionSection</c>.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The section's question names and recorded field values.</returns>
    Task<ProfileSectionAnswers> GetProfileSectionAnswersAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Applies one save or clear to a profile version section in a single transaction, matching the legacy
    /// <c>ProfileVersionSection.Save()</c>: the section row is rewritten via <c>spuProfileVersionSection</c> (which checks
    /// the row version and that the version is a current draft), then the repeating rows are deleted, then the recorded
    /// values are cleared and saved. The user's contribution is logged only when a value changed.
    /// </summary>
    /// <param name="profileVersionId">The profile version being changed.</param>
    /// <param name="profileSectionId">The section being changed.</param>
    /// <param name="change">What to write.</param>
    /// <param name="userId">The acting user, for the contribution log.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The number of recorded values added, replaced or removed.</returns>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">
    /// Thrown when the section was saved by another user since it was read, or the version is not a current draft.
    /// </exception>
    Task<int> ApplyProfileVersionSectionChangeAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        ProfileSectionChange change,
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>Reads every logged contribution to one profile version's section via
    /// <c>spgProfileVersionSectionUser</c>, for the "Contributions" tab.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>Every logged contribution, most recent first.</returns>
    Task<IReadOnlyList<ProfileSectionContribution>> GetProfileVersionSectionContributionsAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken);

    /// <summary>Reads every review comment (and reply) for one profile version's section via
    /// <c>spgReviewCommentByProfileVersionSection</c>, for the "Review comments" tab.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>Every review comment, in thread order (each reply immediately follows its parent).</returns>
    Task<IReadOnlyList<ProfileReviewComment>> GetProfileSectionReviewCommentsAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken);

    /// <summary>Reads a user's logon name and full name by id, so review comments can be attributed.</summary>
    /// <param name="userId">The user to look up.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The user, or <see langword="null"/> when no such user exists.</returns>
    Task<ReviewCommentUser?> GetReviewCommentUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Inserts a review comment (or reply) via <c>spiReviewComment</c>.</summary>
    /// <param name="comment">The comment to insert.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The new comment's row version.</returns>
    Task<byte[]> InsertReviewCommentAsync(NewProfileReviewComment comment, CancellationToken cancellationToken);

    /// <summary>Updates a review comment via <c>spuReviewComment</c>, checking the row version.</summary>
    /// <param name="id">The comment to update.</param>
    /// <param name="subject">The new subject.</param>
    /// <param name="commentReference">The new reference.</param>
    /// <param name="commentText">The new body.</param>
    /// <param name="lastUpdated">The row version the caller last read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The comment's new row version.</returns>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">Thrown when the comment was edited by another user.</exception>
    Task<byte[]> UpdateReviewCommentAsync(
        Guid id,
        string subject,
        string commentReference,
        string commentText,
        byte[] lastUpdated,
        CancellationToken cancellationToken);

    /// <summary>Deletes a review comment via <c>spdReviewComment</c>.</summary>
    /// <param name="id">The comment to delete.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>A task that completes when the comment has been deleted.</returns>
    Task DeleteReviewCommentAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Deletes every review comment, and reply, of one profile version's section via
    /// <c>spdReviewCommentsByProfileSection</c>.</summary>
    /// <param name="profileVersionId">The profile version the comments belong to.</param>
    /// <param name="profileSectionId">The section the comments belong to.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>A task that completes when the comments have been deleted.</returns>
    Task DeleteAllReviewCommentsAsync(Guid profileVersionId, Guid profileSectionId, CancellationToken cancellationToken);

    /// <summary>Reads one section's author/VA/policy review data via <c>spgProfileVersionSection</c>,
    /// for the "All Revision dates" tab.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The section's revision data, or <see langword="null"/> when none is recorded.</returns>
    Task<ProfileSectionRevisionInfo?> GetProfileVersionSectionRevisionInfoAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken);

    /// <summary>Reads the review frequency options via <c>spgaluSectionReviewFrequency</c>.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The options, in sequence order.</returns>
    Task<IReadOnlyList<ProfileReviewLookupItem>> GetReviewFrequenciesAsync(CancellationToken cancellationToken);

    /// <summary>Reads the review status options via <c>spgaluSectionReviewStatusType</c>.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The options, in sequence order.</returns>
    Task<IReadOnlyList<ProfileReviewLookupItem>> GetReviewStatusesAsync(CancellationToken cancellationToken);

    /// <summary>Writes the revision data of several sections of one profile version via
    /// <c>spuProfileVersionSection</c>, all in one transaction, and logs the user's contribution to
    /// each section written.</summary>
    /// <param name="profileVersionId">The profile version being edited.</param>
    /// <param name="updates">The complete revision values to write, one entry per section.</param>
    /// <param name="userId">The user making the change.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The number of sections written.</returns>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">
    /// Thrown when a section was edited by another user, or the profile version is not an editable
    /// current draft.
    /// </exception>
    Task<int> UpdateProfileVersionSectionRevisionsAsync(
        Guid profileVersionId,
        IReadOnlyList<ProfileSectionRevisionUpdate> updates,
        Guid userId,
        CancellationToken cancellationToken);
}
