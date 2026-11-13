using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

/// <summary>An <see cref="IGlobalUsersApiService"/> that lists the users who hold a global role (the authors) it is given, or throws, for pages that only need that list.</summary>
/// <param name="users">The users to list.</param>
/// <param name="throwOnList">The exception to throw instead of listing the users.</param>
internal sealed class StubGlobalUsersApiService(IReadOnlyList<GlobalUserDto> users, Exception? throwOnList = null) : IGlobalUsersApiService
{
    /// <summary>Gets how many times the users who hold a global role were listed.</summary>
    public int RoleHoldersRequests { get; private set; }

    public Task<PagedResult<GlobalUserDto>> GetGlobalUsersAsync(int pageNumber, int pageSize, GlobalUserSortColumn sortColumn, bool sortDescending, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<GlobalUserDto>> GetRoleHoldersAsync(CancellationToken cancellationToken = default)
    {
        RoleHoldersRequests++;

        return throwOnList is not null ? throw throwOnList : Task.FromResult(users);
    }

    public Task<GlobalUserLookupResult> LookupAsync(string userName, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<GlobalUserActionResult> AddAsync(SaveGlobalUserRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<GlobalUserActionResult> UpdateAsync(Guid id, SaveGlobalUserRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<GlobalUserActionResult> DeleteAsync(Guid id, byte[] lastUpdated, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
