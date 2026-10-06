using CDC.Api.Domain.Common;
using CDC.Api.Features.UserAdmin.Dtos;
using CDC.Api.Features.UserAdmin.Interfaces;
using MediatR;

namespace CDC.Api.Features.UserAdmin.Queries;

/// <summary>Requests every internal (global) user account.</summary>
public sealed record GetGlobalUsersQuery : IRequest<Result<IReadOnlyList<MaintainedUserDto>>>;

/// <summary>Handles <see cref="GetGlobalUsersQuery"/>.</summary>
/// <param name="userAdminService">User administration application service.</param>
public sealed class GetGlobalUsersQueryHandler(IUserAdminService userAdminService)
    : IRequestHandler<GetGlobalUsersQuery, Result<IReadOnlyList<MaintainedUserDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The global users.</returns>
    public async Task<Result<IReadOnlyList<MaintainedUserDto>>> Handle(
        GetGlobalUsersQuery request,
        CancellationToken cancellationToken) =>
        Result.Success(await userAdminService.GetGlobalUsersAsync(cancellationToken));
}
