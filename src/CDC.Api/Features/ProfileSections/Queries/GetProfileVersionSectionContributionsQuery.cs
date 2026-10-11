using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Common.Contracts;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileSections.Queries;

/// <summary>
/// Retrieves every logged contribution to one profile version's section, for the "Contributions" tab.
/// </summary>
/// <param name="ProfileVersionId">The profile version to read.</param>
/// <param name="ProfileSectionId">The section to read.</param>
public sealed record GetProfileVersionSectionContributionsQuery(Guid ProfileVersionId, Guid ProfileSectionId)
    : IRequest<Result<IReadOnlyList<ProfileSectionContributionDto>>>;

/// <summary>Validates <see cref="GetProfileVersionSectionContributionsQuery"/>.</summary>
public sealed class GetProfileVersionSectionContributionsQueryValidator : AbstractValidator<GetProfileVersionSectionContributionsQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetProfileVersionSectionContributionsQueryValidator"/> class.</summary>
    public GetProfileVersionSectionContributionsQueryValidator()
    {
        RuleFor(query => query.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");

        RuleFor(query => query.ProfileSectionId)
            .NotEmpty().WithMessage("A profile section id is required.");
    }
}

/// <summary>
/// Handles <see cref="GetProfileVersionSectionContributionsQuery"/>.
/// </summary>
/// <param name="profileSectionService">Profile sections application service.</param>
public sealed class GetProfileVersionSectionContributionsQueryHandler(IProfileSectionService profileSectionService)
    : IRequestHandler<GetProfileVersionSectionContributionsQuery, Result<IReadOnlyList<ProfileSectionContributionDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Every logged contribution, most recent first.</returns>
    public async Task<Result<IReadOnlyList<ProfileSectionContributionDto>>> Handle(
        GetProfileVersionSectionContributionsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await profileSectionService.GetProfileVersionSectionContributionsAsync(
            request.ProfileVersionId, request.ProfileSectionId, cancellationToken);

        return Result.Success(result);
    }
}
