using CDC.Api.Domain.Common;
using CDC.Api.Features.GlobalUsers.Commands;
using CDC.Api.Features.GlobalUsers.Dtos;
using CDC.Common.Contracts;
using MediatR;

namespace CDC.Api.Features.GlobalUsers.Interfaces;

/// <summary>
/// Application service for the global users feature: paging, username lookup, and the business
/// rules the legacy <c>GlobalUser</c> object enforced.
/// </summary>
public interface IGlobalUsersService
{
    /// <summary>Gets one page of global users.</summary>
    /// <param name="pageNumber">The 1-based page to return. Clamped to at least 1.</param>
    /// <param name="pageSize">Items per page, or 0/negative to return every user on a single page.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The requested page of users.</returns>
    Task<PagedResult<GlobalUserDto>> GetGlobalUsersAsync(int pageNumber, int pageSize, CancellationToken cancellationToken);

    /// <summary>Classifies a username for the "Add global user" lookup step and returns the details to pre-fill.</summary>
    /// <param name="userName">The username to look up.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The lookup outcome.</returns>
    Task<GlobalUserLookupResult> LookupAsync(string userName, CancellationToken cancellationToken);

    /// <summary>Validates and saves a global user (add or edit).</summary>
    /// <param name="command">The user to save.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or the validation/concurrency/not-found failure.</returns>
    Task<Result<Unit>> SaveAsync(SaveGlobalUserCommand command, CancellationToken cancellationToken);

    /// <summary>Revokes a user's global role.</summary>
    /// <param name="command">The user to revoke.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Success, or the concurrency failure.</returns>
    Task<Result<Unit>> DeleteAsync(DeleteGlobalUserCommand command, CancellationToken cancellationToken);
}
