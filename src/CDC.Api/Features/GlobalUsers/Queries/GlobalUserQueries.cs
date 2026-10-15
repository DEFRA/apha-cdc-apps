using CDC.Api.Domain.Common;
using CDC.Api.Features.GlobalUsers.Dtos;
using CDC.Api.Features.GlobalUsers.Interfaces;
using CDC.Common.Contracts;
using MediatR;

namespace CDC.Api.Features.GlobalUsers.Queries;

/// <summary>Retrieves one page of global users.</summary>
/// <param name="PageNumber">The 1-based page to return.</param>
/// <param name="PageSize">Items per page, or 0/negative for every user on a single page.</param>
public sealed record GetGlobalUsersQuery(int PageNumber, int PageSize) : IRequest<Result<PagedResult<GlobalUserDto>>>;

/// <summary>Handles <see cref="GetGlobalUsersQuery"/>.</summary>
/// <param name="service">Global users application service.</param>
public sealed class GetGlobalUsersQueryHandler(IGlobalUsersService service)
    : IRequestHandler<GetGlobalUsersQuery, Result<PagedResult<GlobalUserDto>>>
{
    /// <inheritdoc />
    public async Task<Result<PagedResult<GlobalUserDto>>> Handle(GetGlobalUsersQuery request, CancellationToken cancellationToken) =>
        Result.Success(await service.GetGlobalUsersAsync(request.PageNumber, request.PageSize, cancellationToken));
}

/// <summary>Looks a username up for the "Add global user" step. Mirrors the legacy <c>btnLookup_Click</c>.</summary>
/// <param name="UserName">The username to look up.</param>
public sealed record LookupGlobalUserQuery(string UserName) : IRequest<Result<GlobalUserLookupResult>>;

/// <summary>Handles <see cref="LookupGlobalUserQuery"/>.</summary>
/// <param name="service">Global users application service.</param>
public sealed class LookupGlobalUserQueryHandler(IGlobalUsersService service)
    : IRequestHandler<LookupGlobalUserQuery, Result<GlobalUserLookupResult>>
{
    /// <inheritdoc />
    public async Task<Result<GlobalUserLookupResult>> Handle(LookupGlobalUserQuery request, CancellationToken cancellationToken) =>
        Result.Success(await service.LookupAsync(request.UserName, cancellationToken));
}
