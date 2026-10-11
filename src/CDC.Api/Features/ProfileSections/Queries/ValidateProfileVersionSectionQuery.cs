using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Common.Contracts;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileSections.Queries;

/// <summary>
/// Validates one profile version's section, matching the legacy
/// <c>ProfileVersionSection.Validate()</c> mandatory-field check.
/// </summary>
/// <param name="ProfileVersionId">The profile version to validate.</param>
/// <param name="ProfileSectionId">The section to validate.</param>
public sealed record ValidateProfileVersionSectionQuery(Guid ProfileVersionId, Guid ProfileSectionId)
    : IRequest<Result<ProfileSectionValidationResultDto>>;

/// <summary>Validates <see cref="ValidateProfileVersionSectionQuery"/>.</summary>
public sealed class ValidateProfileVersionSectionQueryValidator : AbstractValidator<ValidateProfileVersionSectionQuery>
{
    /// <summary>Initialises a new instance of the <see cref="ValidateProfileVersionSectionQueryValidator"/> class.</summary>
    public ValidateProfileVersionSectionQueryValidator()
    {
        RuleFor(query => query.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");

        RuleFor(query => query.ProfileSectionId)
            .NotEmpty().WithMessage("A profile section id is required.");
    }
}

/// <summary>
/// Handles <see cref="ValidateProfileVersionSectionQuery"/>.
/// </summary>
/// <param name="profileSectionService">Profile sections application service.</param>
public sealed class ValidateProfileVersionSectionQueryHandler(IProfileSectionService profileSectionService)
    : IRequestHandler<ValidateProfileVersionSectionQuery, Result<ProfileSectionValidationResultDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Whether the section is valid, and which mandatory questions are unanswered.</returns>
    public async Task<Result<ProfileSectionValidationResultDto>> Handle(
        ValidateProfileVersionSectionQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await profileSectionService.ValidateProfileVersionSectionAsync(
            request.ProfileVersionId,
            request.ProfileSectionId,
            cancellationToken);

        return Result.Success(result);
    }
}
