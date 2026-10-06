using CDC.Api.Domain.Common;
using CDC.Api.Features.UserAdmin.Dtos;
using CDC.Api.Features.UserAdmin.Interfaces;
using MediatR;

namespace CDC.Api.Features.UserAdmin.Queries;

/// <summary>Requests every external (single sign-on) user account.</summary>
public sealed record GetExternalUsersQuery : IRequest<Result<IReadOnlyList<MaintainedUserDto>>>;

/// <summary>Handles <see cref="GetExternalUsersQuery"/>.</summary>
/// <param name="userAdminService">User administration application service.</param>
public sealed class GetExternalUsersQueryHandler(IUserAdminService userAdminService)
    : IRequestHandler<GetExternalUsersQuery, Result<IReadOnlyList<MaintainedUserDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The external users.</returns>
    public async Task<Result<IReadOnlyList<MaintainedUserDto>>> Handle(
        GetExternalUsersQuery request,
        CancellationToken cancellationToken) =>
        Result.Success(await userAdminService.GetExternalUsersAsync(cancellationToken));
}
