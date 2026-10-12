using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Common.Contracts;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileSections.Queries;

/// <summary>
/// Retrieves one profile version's per-section validation status: whether every mandatory field
/// in each section has a recorded answer.
/// </summary>
/// <param name="ProfileVersionId">The profile version to check.</param>
public sealed record GetProfileVersionValidationStatusQuery(Guid ProfileVersionId)
    : IRequest<Result<ProfileVersionValidationStatusDto>>;

/// <summary>Validates <see cref="GetProfileVersionValidationStatusQuery"/>.</summary>
public sealed class GetProfileVersionValidationStatusQueryValidator : AbstractValidator<GetProfileVersionValidationStatusQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetProfileVersionValidationStatusQueryValidator"/> class.</summary>
    public GetProfileVersionValidationStatusQueryValidator()
    {
        RuleFor(query => query.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");
    }
}

/// <summary>
/// Handles <see cref="GetProfileVersionValidationStatusQuery"/>.
/// </summary>
/// <param name="profileSectionService">Profile sections application service.</param>
public sealed class GetProfileVersionValidationStatusQueryHandler(IProfileSectionService profileSectionService)
    : IRequestHandler<GetProfileVersionValidationStatusQuery, Result<ProfileVersionValidationStatusDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The profile version's per-section validation status.</returns>
    public async Task<Result<ProfileVersionValidationStatusDto>> Handle(
        GetProfileVersionValidationStatusQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var status = await profileSectionService.GetProfileVersionValidationStatusAsync(request.ProfileVersionId, cancellationToken);

        return Result.Success(status);
    }
}
