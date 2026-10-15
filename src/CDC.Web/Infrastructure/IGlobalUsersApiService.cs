using CDC.Common.Contracts;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>Typed client for the global users endpoints on CDC.Api.</summary>
public interface IGlobalUsersApiService
{
    /// <summary>Calls <c>GET /api/global-users</c>.</summary>
    /// <param name="pageNumber">The 1-based page to return.</param>
    /// <param name="pageSize">Items per page, or 0 for every user on a single page.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The requested page of global users.</returns>
    Task<PagedResult<GlobalUserDto>> GetGlobalUsersAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/global-users/lookup</c>.</summary>
    /// <param name="userName">The username to look up.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The lookup outcome and any details to pre-fill.</returns>
    Task<GlobalUserLookupResult> LookupAsync(string userName, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>POST /api/global-users</c>.</summary>
    /// <param name="request">The user to add.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome of the call.</returns>
    Task<GlobalUserActionResult> AddAsync(SaveGlobalUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>PUT /api/global-users/{id}</c>.</summary>
    /// <param name="id">The user being updated.</param>
    /// <param name="request">The change to apply.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome of the call.</returns>
    Task<GlobalUserActionResult> UpdateAsync(Guid id, SaveGlobalUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>DELETE /api/global-users/{id}</c>.</summary>
    /// <param name="id">The user whose global role is revoked.</param>
    /// <param name="lastUpdated">The row version last read for this user.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome of the call.</returns>
    Task<GlobalUserActionResult> DeleteAsync(Guid id, byte[] lastUpdated, CancellationToken cancellationToken = default);
}
