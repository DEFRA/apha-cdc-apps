using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Common.Contracts;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileReports.Queries;

/// <summary>Retrieves the user guidance of a set of questions, for the bespoke report's guidance tables.</summary>
/// <param name="QuestionIds">The questions whose guidance is wanted.</param>
public sealed record GetProfileQuestionGuidanceQuery(IReadOnlyList<Guid> QuestionIds) : IRequest<Result<IReadOnlyList<ProfileQuestionGuidanceDto>>>;

/// <summary>Validates <see cref="GetProfileQuestionGuidanceQuery"/>.</summary>
public sealed class GetProfileQuestionGuidanceQueryValidator : AbstractValidator<GetProfileQuestionGuidanceQuery>
{
    /// <summary>The most questions one request may ask about: the questionnaire has far fewer.</summary>
    public const int MaxQuestions = 1000;

    /// <summary>Initialises a new instance of the <see cref="GetProfileQuestionGuidanceQueryValidator"/> class.</summary>
    public GetProfileQuestionGuidanceQueryValidator()
    {
        RuleFor(query => query.QuestionIds)
            .NotNull().WithMessage("The question ids are required.")
            .Must(ids => ids is null || ids.Count <= MaxQuestions)
            .WithMessage($"At most {MaxQuestions} questions can be requested at once.");

        RuleForEach(query => query.QuestionIds)
            .NotEmpty().WithMessage("A question id cannot be empty.");
    }
}

/// <summary>Handles <see cref="GetProfileQuestionGuidanceQuery"/>.</summary>
/// <param name="contentService">Builds the report content.</param>
public sealed class GetProfileQuestionGuidanceQueryHandler(IProfileReportContentService contentService)
    : IRequestHandler<GetProfileQuestionGuidanceQuery, Result<IReadOnlyList<ProfileQuestionGuidanceDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The guidance.</returns>
    public Task<Result<IReadOnlyList<ProfileQuestionGuidanceDto>>> Handle(GetProfileQuestionGuidanceQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return contentService.GetQuestionGuidanceAsync(request.QuestionIds, cancellationToken);
    }
}
