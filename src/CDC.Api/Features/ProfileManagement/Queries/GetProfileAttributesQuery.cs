using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileManagement.Dtos;
using CDC.Api.Features.ProfileManagement.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileManagement.Queries;

/// <summary>
/// Retrieves a profile's attributes, current version pointers and affected species.
/// </summary>
/// <param name="ProfileId">The profile to read.</param>
public sealed record GetProfileAttributesQuery(Guid ProfileId) : IRequest<Result<ProfileAttributesDto>>;

/// <summary>Validates <see cref="GetProfileAttributesQuery"/>.</summary>
public sealed class GetProfileAttributesQueryValidator : AbstractValidator<GetProfileAttributesQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetProfileAttributesQueryValidator"/> class.</summary>
    public GetProfileAttributesQueryValidator()
    {
        RuleFor(query => query.ProfileId)
            .NotEmpty().WithMessage("A profile id is required.");
    }
}

/// <summary>Handles <see cref="GetProfileAttributesQuery"/>.</summary>
/// <param name="profileManagementService">Profile management application service.</param>
public sealed class GetProfileAttributesQueryHandler(IProfileManagementService profileManagementService)
    : IRequestHandler<GetProfileAttributesQuery, Result<ProfileAttributesDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The profile's attributes, or a not-found result when it does not exist.</returns>
    public async Task<Result<ProfileAttributesDto>> Handle(GetProfileAttributesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var attributes = await profileManagementService.GetProfileAttributesAsync(request.ProfileId, cancellationToken);

        return attributes is null
            ? Result.NotFound<ProfileAttributesDto>($"Profile '{request.ProfileId}' was not found.")
            : Result.Success(attributes);
    }
}
