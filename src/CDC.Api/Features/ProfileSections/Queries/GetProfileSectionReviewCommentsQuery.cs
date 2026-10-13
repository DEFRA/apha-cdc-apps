using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Common.Contracts;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileSections.Queries;

/// <summary>
/// Retrieves every review comment (and reply) for one profile version's section, for the
/// "Review comments" tab.
/// </summary>
/// <param name="ProfileVersionId">The profile version to read.</param>
/// <param name="ProfileSectionId">The section to read.</param>
public sealed record GetProfileSectionReviewCommentsQuery(Guid ProfileVersionId, Guid ProfileSectionId)
    : IRequest<Result<IReadOnlyList<ProfileReviewCommentDto>>>;

/// <summary>Validates <see cref="GetProfileSectionReviewCommentsQuery"/>.</summary>
public sealed class GetProfileSectionReviewCommentsQueryValidator : AbstractValidator<GetProfileSectionReviewCommentsQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetProfileSectionReviewCommentsQueryValidator"/> class.</summary>
    public GetProfileSectionReviewCommentsQueryValidator()
    {
        RuleFor(query => query.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");

        RuleFor(query => query.ProfileSectionId)
            .NotEmpty().WithMessage("A profile section id is required.");
    }
}

/// <summary>
/// Handles <see cref="GetProfileSectionReviewCommentsQuery"/>.
/// </summary>
/// <param name="profileSectionService">Profile sections application service.</param>
public sealed class GetProfileSectionReviewCommentsQueryHandler(IProfileSectionService profileSectionService)
    : IRequestHandler<GetProfileSectionReviewCommentsQuery, Result<IReadOnlyList<ProfileReviewCommentDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Every review comment, in thread order.</returns>
    public async Task<Result<IReadOnlyList<ProfileReviewCommentDto>>> Handle(
        GetProfileSectionReviewCommentsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await profileSectionService.GetProfileSectionReviewCommentsAsync(
            request.ProfileVersionId, request.ProfileSectionId, cancellationToken);

        return Result.Success(result);
    }
}
