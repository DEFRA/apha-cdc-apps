using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileQuestions.Dtos;
using CDC.Api.Features.ProfileQuestions.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileQuestions.Queries;

/// <summary>Retrieves one profile question.</summary>
/// <param name="Id">The question to read.</param>
public sealed record GetProfileQuestionQuery(Guid Id) : IRequest<Result<ProfileQuestionDto>>;

/// <summary>Validates <see cref="GetProfileQuestionQuery"/>.</summary>
public sealed class GetProfileQuestionQueryValidator : AbstractValidator<GetProfileQuestionQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetProfileQuestionQueryValidator"/> class.</summary>
    public GetProfileQuestionQueryValidator()
    {
        RuleFor(query => query.Id)
            .NotEmpty().WithMessage("A question id is required.");
    }
}

/// <summary>Handles <see cref="GetProfileQuestionQuery"/>.</summary>
/// <param name="profileQuestionService">Profile question application service.</param>
public sealed class GetProfileQuestionQueryHandler(IProfileQuestionService profileQuestionService)
    : IRequestHandler<GetProfileQuestionQuery, Result<ProfileQuestionDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The question, or a not-found result when it does not exist.</returns>
    public async Task<Result<ProfileQuestionDto>> Handle(GetProfileQuestionQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var question = await profileQuestionService.GetProfileQuestionAsync(request.Id, cancellationToken);

        return question is null
            ? Result.NotFound<ProfileQuestionDto>($"Profile question '{request.Id}' was not found.")
            : Result.Success(question);
    }
}
