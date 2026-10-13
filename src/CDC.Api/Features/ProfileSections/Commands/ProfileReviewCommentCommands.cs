using System.Linq.Expressions;
using CDC.Api.Domain.Common;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Common.Contracts;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileSections.Commands;

/// <summary>
/// Adds a review comment to one profile version's section, or a reply when
/// <see cref="ParentId"/> is supplied. Matches the legacy "Add review comment" / "Reply" panel.
/// </summary>
public sealed record AddProfileReviewCommentCommand : IRequest<Result<SaveProfileReviewCommentResultDto>>
{
    /// <summary>Gets the profile version being commented on.</summary>
    public required Guid ProfileVersionId { get; init; }

    /// <summary>Gets the section being commented on.</summary>
    public required Guid ProfileSectionId { get; init; }

    /// <summary>Gets the comment being replied to, or <see langword="null"/> for a top-level comment.</summary>
    public Guid? ParentId { get; init; }

    /// <summary>Gets the subject line.</summary>
    public required string Subject { get; init; }

    /// <summary>Gets the reference text.</summary>
    public required string CommentReference { get; init; }

    /// <summary>Gets the comment body (HTML from the link-only rich text editor).</summary>
    public required string CommentText { get; init; }
}

/// <summary>Validation messages shared by the add and edit review comment commands (legacy wording).</summary>
internal static class ReviewCommentRules
{
    public const int MaximumTextLength = 255;

    public static void Apply<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, string>> subject,
        Expression<Func<T, string>> reference,
        Expression<Func<T, string>> comment)
    {
        validator.RuleFor(subject)
            .NotEmpty().WithMessage("You must enter a subject or cancel")
            .MaximumLength(MaximumTextLength).WithMessage("A subject must be no longer than 255 characters");

        validator.RuleFor(reference)
            .NotEmpty().WithMessage("You must enter a reference or cancel")
            .MaximumLength(MaximumTextLength).WithMessage("A reference must be no longer than 255 characters");

        validator.RuleFor(comment)
            .NotEmpty().WithMessage("You must enter a comment or cancel");
    }
}

/// <summary>Validates <see cref="AddProfileReviewCommentCommand"/>.</summary>
public sealed class AddProfileReviewCommentCommandValidator : AbstractValidator<AddProfileReviewCommentCommand>
{
    /// <summary>Initialises a new instance of the <see cref="AddProfileReviewCommentCommandValidator"/> class.</summary>
    public AddProfileReviewCommentCommandValidator()
    {
        RuleFor(command => command.ProfileVersionId).NotEmpty().WithMessage("A profile version id is required.");
        RuleFor(command => command.ProfileSectionId).NotEmpty().WithMessage("A profile section id is required.");
        RuleFor(command => command.ParentId).NotEqual(Guid.Empty).WithMessage("The parent comment id is not valid.");

        ReviewCommentRules.Apply(this, command => command.Subject, command => command.CommentReference, command => command.CommentText);
    }
}

/// <summary>
/// Handles <see cref="AddProfileReviewCommentCommand"/>.
/// </summary>
/// <param name="reviewCommentService">Review comment application service.</param>
public sealed class AddProfileReviewCommentCommandHandler(IProfileReviewCommentService reviewCommentService)
    : IRequestHandler<AddProfileReviewCommentCommand, Result<SaveProfileReviewCommentResultDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The new comment's id and row version, or a forbidden/not-found result.</returns>
    public async Task<Result<SaveProfileReviewCommentResultDto>> Handle(
        AddProfileReviewCommentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await reviewCommentService.AddAsync(
            request.ProfileVersionId,
            request.ProfileSectionId,
            request.ParentId,
            request.Subject,
            request.CommentReference,
            request.CommentText,
            cancellationToken);
    }
}

/// <summary>
/// Edits a review comment the acting user owns and that has no replies. Matches the legacy
/// "Edit review comment" panel.
/// </summary>
public sealed record UpdateProfileReviewCommentCommand : IRequest<Result<SaveProfileReviewCommentResultDto>>
{
    /// <summary>Gets the comment being edited.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the profile version the comment belongs to.</summary>
    public required Guid ProfileVersionId { get; init; }

    /// <summary>Gets the section the comment belongs to.</summary>
    public required Guid ProfileSectionId { get; init; }

    /// <summary>Gets the new subject line.</summary>
    public required string Subject { get; init; }

    /// <summary>Gets the new reference text.</summary>
    public required string CommentReference { get; init; }

    /// <summary>Gets the new comment body.</summary>
    public required string CommentText { get; init; }

    /// <summary>Gets the row version the caller last read, used to detect a concurrent edit.</summary>
    public required byte[] LastUpdated { get; init; }
}

/// <summary>Validates <see cref="UpdateProfileReviewCommentCommand"/>.</summary>
public sealed class UpdateProfileReviewCommentCommandValidator : AbstractValidator<UpdateProfileReviewCommentCommand>
{
    private const int RowVersionLength = 8;

    /// <summary>Initialises a new instance of the <see cref="UpdateProfileReviewCommentCommandValidator"/> class.</summary>
    public UpdateProfileReviewCommentCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty().WithMessage("A review comment id is required.");
        RuleFor(command => command.ProfileVersionId).NotEmpty().WithMessage("A profile version id is required.");
        RuleFor(command => command.ProfileSectionId).NotEmpty().WithMessage("A profile section id is required.");
        RuleFor(command => command.LastUpdated)
            .NotNull().Must(value => value is { Length: RowVersionLength })
            .WithMessage("The review comment's row version is required.");

        ReviewCommentRules.Apply(this, command => command.Subject, command => command.CommentReference, command => command.CommentText);
    }
}

/// <summary>
/// Handles <see cref="UpdateProfileReviewCommentCommand"/>.
/// </summary>
/// <param name="reviewCommentService">Review comment application service.</param>
/// <param name="logger">Structured logger.</param>
public sealed class UpdateProfileReviewCommentCommandHandler(
    IProfileReviewCommentService reviewCommentService,
    ILogger<UpdateProfileReviewCommentCommandHandler> logger)
    : IRequestHandler<UpdateProfileReviewCommentCommand, Result<SaveProfileReviewCommentResultDto>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The comment's new row version, or a conflict/forbidden/not-found result.</returns>
    public async Task<Result<SaveProfileReviewCommentResultDto>> Handle(
        UpdateProfileReviewCommentCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            return await reviewCommentService.UpdateAsync(
                request.ProfileVersionId,
                request.ProfileSectionId,
                request.Id,
                request.Subject,
                request.CommentReference,
                request.CommentText,
                request.LastUpdated,
                cancellationToken);
        }
        catch (ConcurrencyException exception)
        {
            logger.ReviewCommentConcurrencyConflict(request.Id);

            return Result.Conflict<SaveProfileReviewCommentResultDto>(exception.Message);
        }
    }
}

/// <summary>
/// Deletes a review comment the acting user owns and that has no replies.
/// </summary>
/// <param name="Id">The comment to delete.</param>
/// <param name="ProfileVersionId">The profile version the comment belongs to.</param>
/// <param name="ProfileSectionId">The section the comment belongs to.</param>
public sealed record DeleteProfileReviewCommentCommand(Guid Id, Guid ProfileVersionId, Guid ProfileSectionId)
    : IRequest<Result<Unit>>;

/// <summary>Validates <see cref="DeleteProfileReviewCommentCommand"/>.</summary>
public sealed class DeleteProfileReviewCommentCommandValidator : AbstractValidator<DeleteProfileReviewCommentCommand>
{
    /// <summary>Initialises a new instance of the <see cref="DeleteProfileReviewCommentCommandValidator"/> class.</summary>
    public DeleteProfileReviewCommentCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty().WithMessage("A review comment id is required.");
        RuleFor(command => command.ProfileVersionId).NotEmpty().WithMessage("A profile version id is required.");
        RuleFor(command => command.ProfileSectionId).NotEmpty().WithMessage("A profile section id is required.");
    }
}

/// <summary>
/// Handles <see cref="DeleteProfileReviewCommentCommand"/>.
/// </summary>
/// <param name="reviewCommentService">Review comment application service.</param>
public sealed class DeleteProfileReviewCommentCommandHandler(IProfileReviewCommentService reviewCommentService)
    : IRequestHandler<DeleteProfileReviewCommentCommand, Result<Unit>>
{
    /// <summary>Executes the command.</summary>
    /// <param name="request">The command.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or a forbidden/not-found result.</returns>
    public async Task<Result<Unit>> Handle(DeleteProfileReviewCommentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await reviewCommentService.DeleteAsync(
            request.ProfileVersionId, request.ProfileSectionId, request.Id, cancellationToken);
    }
}
