using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileQuestions.Dtos;
using CDC.Api.Features.ProfileQuestions.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileQuestions.Queries;

/// <summary>
/// Retrieves the questions within one profile section. Mirrors the legacy
/// <c>GetProfileQuestionInfoListRequest</c> data contract, which is keyed by section rather
/// than parameterless.
/// </summary>
/// <param name="ProfileSectionId">The section to read.</param>
public sealed record GetProfileQuestionInfoListQuery(Guid ProfileSectionId) : IRequest<Result<IReadOnlyList<ProfileQuestionInfoDto>>>;

/// <summary>Validates <see cref="GetProfileQuestionInfoListQuery"/>.</summary>
public sealed class GetProfileQuestionInfoListQueryValidator : AbstractValidator<GetProfileQuestionInfoListQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetProfileQuestionInfoListQueryValidator"/> class.</summary>
    public GetProfileQuestionInfoListQueryValidator()
    {
        RuleFor(query => query.ProfileSectionId)
            .NotEmpty().WithMessage("A profile section id is required.");
    }
}

/// <summary>Handles <see cref="GetProfileQuestionInfoListQuery"/>.</summary>
/// <param name="profileQuestionService">Profile question application service.</param>
public sealed class GetProfileQuestionInfoListQueryHandler(IProfileQuestionService profileQuestionService)
    : IRequestHandler<GetProfileQuestionInfoListQuery, Result<IReadOnlyList<ProfileQuestionInfoDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The section's questions; empty when the section has none.</returns>
    public async Task<Result<IReadOnlyList<ProfileQuestionInfoDto>>> Handle(
        GetProfileQuestionInfoListQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var questions = await profileQuestionService.GetProfileQuestionInfoListAsync(request.ProfileSectionId, cancellationToken);

        return Result.Success(questions);
    }
}
