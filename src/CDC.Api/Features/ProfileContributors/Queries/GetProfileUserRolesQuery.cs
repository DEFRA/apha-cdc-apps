using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileContributors.Dtos;
using CDC.Api.Features.ProfileContributors.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileContributors.Queries;

/// <summary>
/// Retrieves every role a contributor can hold on a profile.
/// </summary>
public sealed record GetProfileUserRolesQuery : IRequest<Result<IReadOnlyList<ProfileUserRoleDto>>>;

/// <summary>Handles <see cref="GetProfileUserRolesQuery"/>.</summary>
/// <param name="profileContributorsService">Profile contributors application service.</param>
public sealed class GetProfileUserRolesQueryHandler(IProfileContributorsService profileContributorsService)
    : IRequestHandler<GetProfileUserRolesQuery, Result<IReadOnlyList<ProfileUserRoleDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Every contributor role.</returns>
    public async Task<Result<IReadOnlyList<ProfileUserRoleDto>>> Handle(
        GetProfileUserRolesQuery request,
        CancellationToken cancellationToken)
    {
        var roles = await profileContributorsService.GetProfileUserRolesAsync(cancellationToken);

        return Result.Success(roles);
    }
}
