using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileManagement.Dtos;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Queries;

/// <summary>Retrieves the details shown on the "Manage profile" page for one profile.</summary>
/// <param name="ProfileId">The profile to read.</param>
public sealed record GetManageProfileQuery(Guid ProfileId) : IRequest<Result<GetManageProfileResponse>>;

/// <summary>Validates <see cref="GetManageProfileQuery"/>.</summary>
public sealed class GetManageProfileQueryValidator : AbstractValidator<GetManageProfileQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetManageProfileQueryValidator"/> class.</summary>
    public GetManageProfileQueryValidator()
    {
        RuleFor(query => query.ProfileId)
            .NotEmpty().WithMessage("A profile id is required.");
    }
}
