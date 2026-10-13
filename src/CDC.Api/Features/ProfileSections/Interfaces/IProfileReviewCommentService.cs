using CDC.Api.Domain.Common;
using CDC.Common.Contracts;
using MediatR;

namespace CDC.Api.Features.ProfileSections.Interfaces;

/// <summary>
/// Application service for a profile section's review comments (the "Review comments" tab):
/// reading the threaded comments and adding, replying to, editing and deleting them.
/// </summary>
public interface IProfileReviewCommentService
{
    /// <summary>Gets every review comment (and reply) for one profile version's section, each
    /// flagged with whether the acting user may edit or delete it.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Every review comment, in thread order.</returns>
    Task<IReadOnlyList<ProfileReviewCommentDto>> GetAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken);

    /// <summary>Adds a top-level comment, or a reply when <paramref name="parentId"/> is supplied.</summary>
    /// <param name="profileVersionId">The profile version being commented on.</param>
    /// <param name="profileSectionId">The section being commented on.</param>
    /// <param name="parentId">The comment being replied to, or <see langword="null"/> for a top-level comment.</param>
    /// <param name="subject">The subject line.</param>
    /// <param name="commentReference">The reference text.</param>
    /// <param name="commentText">The comment body.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new comment's id and row version; forbidden when the version is not an editable
    /// current draft; not found when the parent comment does not exist.</returns>
    Task<Result<SaveProfileReviewCommentResultDto>> AddAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid? parentId,
        string subject,
        string commentReference,
        string commentText,
        CancellationToken cancellationToken);

    /// <summary>Edits a comment the acting user owns and that has no replies.</summary>
    /// <param name="profileVersionId">The profile version the comment belongs to.</param>
    /// <param name="profileSectionId">The section the comment belongs to.</param>
    /// <param name="commentId">The comment to edit.</param>
    /// <param name="content">The new subject, reference and body.</param>
    /// <param name="lastUpdated">The row version the caller last read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The comment's new row version; not found when it does not exist; forbidden when it cannot be edited.</returns>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">Thrown when the comment was edited by another user.</exception>
    Task<Result<SaveProfileReviewCommentResultDto>> UpdateAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid commentId,
        ReviewCommentContent content,
        byte[] lastUpdated,
        CancellationToken cancellationToken);

    /// <summary>Deletes a comment the acting user owns and that has no replies.</summary>
    /// <param name="profileVersionId">The profile version the comment belongs to.</param>
    /// <param name="profileSectionId">The section the comment belongs to.</param>
    /// <param name="commentId">The comment to delete.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success; not found when it does not exist; forbidden when it cannot be deleted.</returns>
    Task<Result<Unit>> DeleteAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid commentId,
        CancellationToken cancellationToken);
}
