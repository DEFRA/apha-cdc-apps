using CDC.Api.Domain.Common;
using CDC.Api.Features.UserAdmin.Dtos;
using CDC.Api.Features.UserAdmin.Interfaces;
using MediatR;

namespace CDC.Api.Features.UserAdmin.Queries;

/// <summary>Requests one user account.</summary>
/// <param name="UserId">The user to read.</param>
public sealed record GetMaintainedUserQuery(Guid UserId) : IRequest<Result<MaintainedUserDto>>;

/// <summary>Handles <see cref="GetMaintainedUserQuery"/>.</summary>
/// <param name="userAdminService">User administration application service.</param>
public sealed class GetMaintainedUserQueryHandler(IUserAdminService userAdminService)
    : IRequestHandler<GetMaintainedUserQuery, Result<MaintainedUserDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The user, or a not-found result.</returns>
    public async Task<Result<MaintainedUserDto>> Handle(
        GetMaintainedUserQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var user = await userAdminService.GetUserAsync(request.UserId, cancellationToken);

        return user is null
            ? Result.NotFound<MaintainedUserDto>($"No user exists with id '{request.UserId}'.")
            : Result.Success(user);
    }
}
