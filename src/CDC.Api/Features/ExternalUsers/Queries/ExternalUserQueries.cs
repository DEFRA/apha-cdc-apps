using CDC.Api.Domain.Common;
using CDC.Api.Features.ExternalUsers.Dtos;
using CDC.Api.Features.ExternalUsers.Interfaces;
using CDC.Common.Contracts;
using MediatR;

namespace CDC.Api.Features.ExternalUsers.Queries;

/// <summary>Retrieves one page of external users.</summary>
/// <param name="PageNumber">The 1-based page to return.</param>
/// <param name="PageSize">Items per page, or 0/negative for every user on a single page.</param>
public sealed record GetExternalUsersQuery(int PageNumber, int PageSize) : IRequest<Result<PagedResult<ExternalUserDto>>>;

/// <summary>Handles <see cref="GetExternalUsersQuery"/>.</summary>
/// <param name="service">External users application service.</param>
public sealed class GetExternalUsersQueryHandler(IExternalUsersService service)
    : IRequestHandler<GetExternalUsersQuery, Result<PagedResult<ExternalUserDto>>>
{
    /// <inheritdoc />
    public async Task<Result<PagedResult<ExternalUserDto>>> Handle(GetExternalUsersQuery request, CancellationToken cancellationToken) =>
        Result.Success(await service.GetExternalUsersAsync(request.PageNumber, request.PageSize, cancellationToken));
}

/// <summary>Retrieves the profiles an external user can access, and those still available to grant.</summary>
/// <param name="UserId">The external user.</param>
public sealed record GetExternalUserProfileAccessQuery(Guid UserId) : IRequest<Result<ExternalUserProfileAccessContract>>;

/// <summary>Handles <see cref="GetExternalUserProfileAccessQuery"/>.</summary>
/// <param name="service">External users application service.</param>
public sealed class GetExternalUserProfileAccessQueryHandler(IExternalUsersService service)
    : IRequestHandler<GetExternalUserProfileAccessQuery, Result<ExternalUserProfileAccessContract>>
{
    /// <inheritdoc />
    public Task<Result<ExternalUserProfileAccessContract>> Handle(GetExternalUserProfileAccessQuery request, CancellationToken cancellationToken) =>
        service.GetProfileAccessAsync(request.UserId, cancellationToken);
}
