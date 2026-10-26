using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

/// <summary>An <see cref="IGlobalUsersApiService"/> that lists the users it is given, or throws, for pages that only need the list.</summary>
/// <param name="users">The users to list.</param>
/// <param name="throwOnList">The exception to throw instead of listing the users.</param>
internal sealed class StubGlobalUsersApiService(IReadOnlyList<GlobalUserDto> users, Exception? throwOnList = null) : IGlobalUsersApiService
{
    /// <summary>Gets the page number and size most recently asked for.</summary>
    public (int PageNumber, int PageSize)? RequestedPage { get; private set; }

    public Task<PagedResult<GlobalUserDto>> GetGlobalUsersAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        RequestedPage = (pageNumber, pageSize);

        return throwOnList is not null
            ? throw throwOnList
            : Task.FromResult(new PagedResult<GlobalUserDto> { Items = users, PageNumber = pageNumber, PageSize = pageSize, TotalRecords = users.Count });
    }

    public Task<GlobalUserLookupResult> LookupAsync(string userName, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<GlobalUserActionResult> AddAsync(SaveGlobalUserRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<GlobalUserActionResult> UpdateAsync(Guid id, SaveGlobalUserRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<GlobalUserActionResult> DeleteAsync(Guid id, byte[] lastUpdated, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
