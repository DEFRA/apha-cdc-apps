using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Common.Contracts;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileReports.Queries;

/// <summary>Retrieves the prioritisation scores the summary prioritisation report prints for a profile version.</summary>
/// <param name="ProfileVersionId">The profile version to read.</param>
public sealed record GetProfilePrioritisationScoresQuery(Guid ProfileVersionId) : IRequest<Result<ProfilePrioritisationScoresDto>>;

/// <summary>Validates <see cref="GetProfilePrioritisationScoresQuery"/>.</summary>
public sealed class GetProfilePrioritisationScoresQueryValidator : AbstractValidator<GetProfilePrioritisationScoresQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetProfilePrioritisationScoresQueryValidator"/> class.</summary>
    public GetProfilePrioritisationScoresQueryValidator()
    {
        RuleFor(query => query.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");
    }
}

/// <summary>Handles <see cref="GetProfilePrioritisationScoresQuery"/>.</summary>
/// <param name="contentService">Builds the report content.</param>
public sealed class GetProfilePrioritisationScoresQueryHandler(IProfileReportContentService contentService)
    : IRequestHandler<GetProfilePrioritisationScoresQuery, Result<ProfilePrioritisationScoresDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The scores.</returns>
    public Task<Result<ProfilePrioritisationScoresDto>> Handle(GetProfilePrioritisationScoresQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return contentService.GetProfilePrioritisationScoresAsync(request.ProfileVersionId, cancellationToken);
    }
}
