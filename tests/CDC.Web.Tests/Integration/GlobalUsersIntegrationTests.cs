using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// The default test host has no live CDC.Api, so the table would only ever render its error state; a fake supplies the users.
public class GlobalUsersIntegrationTests
{
    [Fact]
    public async Task GlobalUsers_OffersDeleteOnlyToUsersWhoHoldAGlobalRole()
    {
        var holder = User("Has Role", GlobalUserRole.ProfileEditor);
        var noRole = User("No Role", GlobalUserRole.None);
        var users = new PagedResult<GlobalUserDto> { Items = [holder, noRole], PageNumber = 1, PageSize = 10, TotalRecords = 2 };

        using var factory = new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGlobalUsersApiService>();
                services.AddSingleton<IGlobalUsersApiService>(new FakeGlobalUsersApiService(users));
            }));
        var client = await factory.SignedInClientAsync();

        var body = await client.GetStringAsync("/UserAdmin/GlobalUsers");

        Assert.Contains($"EditUserId={holder.Id}", body, StringComparison.Ordinal);
        Assert.Contains($"EditUserId={noRole.Id}", body, StringComparison.Ordinal);
        Assert.Contains($"id=\"delete-global-user-{holder.Id}\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain($"delete-global-user-{noRole.Id}", body, StringComparison.Ordinal);
    }

    private static GlobalUserDto User(string fullName, GlobalUserRole role) => new()
    {
        Id = Guid.NewGuid(),
        UserName = $@"DEFRA\{fullName.Replace(' ', '.')}",
        FullName = fullName,
        Organisation = "APHA",
        Role = role,
        LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
    };

    private sealed class FakeGlobalUsersApiService(PagedResult<GlobalUserDto> users) : IGlobalUsersApiService
    {
        public Task<PagedResult<GlobalUserDto>> GetGlobalUsersAsync(
            int pageNumber, int pageSize, GlobalUserSortColumn sortColumn, bool sortDescending, CancellationToken cancellationToken = default) =>
            Task.FromResult(users);

        public Task<IReadOnlyList<GlobalUserDto>> GetRoleHoldersAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GlobalUserLookupResult> LookupAsync(string userName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GlobalUserActionResult> AddAsync(SaveGlobalUserRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GlobalUserActionResult> UpdateAsync(Guid id, SaveGlobalUserRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GlobalUserActionResult> DeleteAsync(Guid id, byte[] lastUpdated, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
