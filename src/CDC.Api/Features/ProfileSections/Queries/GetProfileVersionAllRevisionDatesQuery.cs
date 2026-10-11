using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Common.Contracts;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileSections.Queries;

/// <summary>
/// Retrieves every section's technical/policy review dates for one profile version, for the
/// "All Revision dates" tab.
/// </summary>
/// <param name="ProfileVersionId">The profile version to read.</param>
public sealed record GetProfileVersionAllRevisionDatesQuery(Guid ProfileVersionId)
    : IRequest<Result<ProfileVersionAllRevisionDatesDto>>;

/// <summary>Validates <see cref="GetProfileVersionAllRevisionDatesQuery"/>.</summary>
public sealed class GetProfileVersionAllRevisionDatesQueryValidator : AbstractValidator<GetProfileVersionAllRevisionDatesQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetProfileVersionAllRevisionDatesQueryValidator"/> class.</summary>
    public GetProfileVersionAllRevisionDatesQueryValidator()
    {
        RuleFor(query => query.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");
    }
}

/// <summary>
/// Handles <see cref="GetProfileVersionAllRevisionDatesQuery"/>.
/// </summary>
/// <param name="profileSectionService">Profile sections application service.</param>
public sealed class GetProfileVersionAllRevisionDatesQueryHandler(IProfileSectionService profileSectionService)
    : IRequestHandler<GetProfileVersionAllRevisionDatesQuery, Result<ProfileVersionAllRevisionDatesDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Each section's revision dates, in section-number order.</returns>
    public async Task<Result<ProfileVersionAllRevisionDatesDto>> Handle(
        GetProfileVersionAllRevisionDatesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await profileSectionService.GetProfileVersionAllRevisionDatesAsync(request.ProfileVersionId, cancellationToken);

        return Result.Success(result);
    }
}
