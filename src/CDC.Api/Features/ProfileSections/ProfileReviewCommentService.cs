using CDC.Api.Application;
using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Common.Contracts;
using MediatR;

namespace CDC.Api.Features.ProfileSections;

/// <summary>
/// Default <see cref="IProfileReviewCommentService"/>. Matches the legacy <c>ReviewComment</c>
/// rules: comments can be read by anyone who can view the version, can be added, edited and deleted only by a profile editor
/// while the profile version is the current draft, and a comment can be edited or deleted only by its author and only while
/// it has no replies.
/// </summary>
/// <param name="repository">Profile section data access.</param>
/// <param name="versions">Reads a version's state and refuses what the signed-in user's role does not allow.</param>
/// <param name="userContext">The signed-in user: their roles, and the author a comment is attributed to.</param>
/// <param name="logger">Structured logger.</param>
/// <param name="timeProvider">Supplies the comment time; defaults to the system clock.</param>
public sealed class ProfileReviewCommentService(
    IProfileSectionRepository repository,
    IProfileVersionGuard versions,
    IUserContext userContext,
    ILogger<ProfileReviewCommentService> logger,
    TimeProvider? timeProvider = null)
    : IProfileReviewCommentService
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileReviewCommentDto>> GetAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        await versions.EnsureCanViewNotesAsync(profileVersionId, cancellationToken);

        var comments = await repository.GetProfileSectionReviewCommentsAsync(profileVersionId, profileSectionId, cancellationToken);
        var actingUser = comments.Count > 0 ? await ResolveEditingUserAsync(profileVersionId, cancellationToken) : null;

        return [.. comments.Select(comment =>
        {
            var canModify = actingUser is not null && CanModify(comment, actingUser);

            return new ProfileReviewCommentDto
            {
                Id = comment.Id,
                CommentDate = comment.CommentDate,
                CommentSubject = comment.CommentSubject,
                CommentReference = comment.CommentReference,
                CommentText = comment.CommentText,
                UserName = comment.UserName,
                UserFullName = comment.UserFullName,
                ParentId = comment.ParentId,
                Level = comment.Level,
                ChildCount = comment.ChildCount,
                LastUpdated = comment.LastUpdated,
                CanEdit = canModify,
                CanDelete = canModify
            };
        })];
    }

    /// <inheritdoc />
    public async Task<Result<SaveProfileReviewCommentResultDto>> AddAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid? parentId,
        string subject,
        string commentReference,
        string commentText,
        CancellationToken cancellationToken)
    {
        var actingUser = await ResolveEditingUserAsync(profileVersionId, cancellationToken);

        if (actingUser is null)
        {
            logger.ReviewCommentChangeRefused(profileVersionId, "not an editable current draft, or the acting user is unknown");

            return Result.Forbidden<SaveProfileReviewCommentResultDto>("Review comments can only be added to the current draft version.");
        }

        if (parentId is not null)
        {
            var comments = await repository.GetProfileSectionReviewCommentsAsync(profileVersionId, profileSectionId, cancellationToken);

            if (comments.All(comment => comment.Id != parentId.Value))
            {
                return Result.NotFound<SaveProfileReviewCommentResultDto>("The review comment being replied to does not exist.");
            }
        }

        var newComment = new NewProfileReviewComment
        {
            Id = Guid.NewGuid(),
            ProfileVersionId = profileVersionId,
            ProfileSectionId = profileSectionId,
            ParentId = parentId,
            CommentDate = clock.GetLocalNow().DateTime,
            Subject = subject.Trim(),
            CommentReference = commentReference.Trim(),
            CommentText = commentText,
            UserName = actingUser.UserName
        };

        var lastUpdated = await repository.InsertReviewCommentAsync(newComment, cancellationToken);
        logger.AddedReviewComment(newComment.Id, profileVersionId, profileSectionId);

        return Result.Success(new SaveProfileReviewCommentResultDto { Id = newComment.Id, LastUpdated = lastUpdated });
    }

    /// <inheritdoc />
    public async Task<Result<SaveProfileReviewCommentResultDto>> UpdateAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid commentId,
        ReviewCommentContent content,
        byte[] lastUpdated,
        CancellationToken cancellationToken)
    {
        var check = await CheckCanModifyAsync(profileVersionId, profileSectionId, commentId, cancellationToken);

        if (check is not null)
        {
            return check.Status == ResultStatus.NotFound
                ? Result.NotFound<SaveProfileReviewCommentResultDto>(check.Error!)
                : Result.Forbidden<SaveProfileReviewCommentResultDto>(check.Error!);
        }

        var newLastUpdated = await repository.UpdateReviewCommentAsync(
            commentId, content.Subject.Trim(), content.CommentReference.Trim(), content.CommentText, lastUpdated, cancellationToken);
        logger.UpdatedReviewComment(commentId);

        return Result.Success(new SaveProfileReviewCommentResultDto { Id = commentId, LastUpdated = newLastUpdated });
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> DeleteAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var check = await CheckCanModifyAsync(profileVersionId, profileSectionId, commentId, cancellationToken);

        if (check is not null)
        {
            return check.Status == ResultStatus.NotFound
                ? Result.NotFound<Unit>(check.Error!)
                : Result.Forbidden<Unit>(check.Error!);
        }

        await repository.DeleteReviewCommentAsync(commentId, cancellationToken);
        logger.DeletedReviewComment(commentId);

        return Result.Success(Unit.Value);
    }

    /// <inheritdoc />
    public async Task<Result<Unit>> DeleteAllAsync(Guid profileVersionId, Guid profileSectionId, CancellationToken cancellationToken)
    {
        var version = await versions.FindAsync(profileVersionId, cancellationToken);

        if (version is not { } facts || !ProfileAccess.CanWriteReviewComments(userContext, facts))
        {
            logger.ReviewCommentChangeRefused(profileVersionId, "not an editable current draft, or not a profile editor");

            return Result.Forbidden<Unit>("Review comments can only be deleted from the current draft version.");
        }

        await repository.DeleteAllReviewCommentsAsync(profileVersionId, profileSectionId, cancellationToken);
        logger.DeletedAllReviewComments(profileVersionId, profileSectionId);

        return Result.Success(Unit.Value);
    }

    private static bool CanModify(ProfileReviewComment comment, ReviewCommentUser actingUser) =>
        comment.ChildCount == 0 && string.Equals(comment.UserName, actingUser.UserName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Returns a failure description when the comment cannot be modified, otherwise <see langword="null"/>.</summary>
    private async Task<ModifyRefusal?> CheckCanModifyAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid commentId,
        CancellationToken cancellationToken)
    {
        var comments = await repository.GetProfileSectionReviewCommentsAsync(profileVersionId, profileSectionId, cancellationToken);
        var comment = comments.FirstOrDefault(item => item.Id == commentId);

        if (comment is null)
        {
            return new ModifyRefusal(ResultStatus.NotFound, "The review comment does not exist.");
        }

        var actingUser = await ResolveEditingUserAsync(profileVersionId, cancellationToken);

        if (actingUser is null || !CanModify(comment, actingUser))
        {
            logger.ReviewCommentChangeRefused(profileVersionId, "the comment is not the acting user's own, has replies, or the version is not an editable current draft");

            return new ModifyRefusal(ResultStatus.Forbidden, "Only your own review comment, with no replies, can be changed on the current draft version.");
        }

        return null;
    }

    /// <summary>Resolves the acting user, or <see langword="null"/> when the version is not a
    /// current draft they may write comments on or they cannot be found.</summary>
    private async Task<ReviewCommentUser?> ResolveEditingUserAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        var version = await versions.FindAsync(profileVersionId, cancellationToken);

        if (version is not { } facts || !ProfileAccess.CanWriteReviewComments(userContext, facts) || userContext.UserId is not { } userId)
        {
            return null;
        }

        return await repository.GetReviewCommentUserAsync(userId, cancellationToken);
    }

    private sealed record ModifyRefusal(ResultStatus Status, string? Error);
}
