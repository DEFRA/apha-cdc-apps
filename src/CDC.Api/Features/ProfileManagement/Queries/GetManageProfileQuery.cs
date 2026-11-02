using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileManagement.Dtos;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Queries;

/// <summary>Retrieves the details shown on the "Manage profile" page for one profile.</summary>
/// <param name="ProfileId">The profile to read.</param>
/// <param name="ProfileVersionId">The version the caller is about to browse, when it followed that version's link. It does not choose
/// what is returned; it lets the request be refused when it is a draft the user may not read.</param>
public sealed record GetManageProfileQuery(Guid ProfileId, Guid? ProfileVersionId = null) : IRequest<Result<GetManageProfileResponse>>;

/// <summary>Validates <see cref="GetManageProfileQuery"/>.</summary>
public sealed class GetManageProfileQueryValidator : AbstractValidator<GetManageProfileQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetManageProfileQueryValidator"/> class.</summary>
    public GetManageProfileQueryValidator()
    {
        RuleFor(query => query.ProfileId)
            .NotEmpty().WithMessage("A profile id is required.");

        RuleFor(query => query.ProfileVersionId)
            .NotEqual(Guid.Empty).When(query => query.ProfileVersionId.HasValue)
            .WithMessage("A profile version id must not be empty.");
    }
}
