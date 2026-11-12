using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// The default test host has no live CDC.Api, so the table would only ever render its error state; a fake supplies the users.
public class ExternalUsersIntegrationTests
{
    [Fact]
    public async Task ExternalUsers_ShowsEachUsersEmailInASortableColumnBetweenFullNameAndOrganisation()
    {
        var users = new PagedResult<MaintainedExternalUserDto>
        {
            Items =
            [
                new MaintainedExternalUserDto
                {
                    Id = Guid.NewGuid(),
                    UserName = "jsmith",
                    FullName = "Jo Smith",
                    EmailAddress = "jo.smith@example.test",
                    Organisation = "Acme Ltd"
                }
            ],
            PageNumber = 1,
            PageSize = 10,
            TotalRecords = 1
        };

        using var factory = new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IExternalUsersApiService>();
                services.AddSingleton<IExternalUsersApiService>(new FakeExternalUsersApiService(users));
            }));
        var client = await factory.SignedInClientAsync();

        var body = await client.GetStringAsync("/UserAdmin/ExternalUsers");

        var fullNameHeader = body.IndexOf(">Full name</a>", StringComparison.Ordinal);
        var emailHeader = body.IndexOf(">Email</a>", StringComparison.Ordinal);
        var organisationHeader = body.IndexOf(">Organisation</a>", StringComparison.Ordinal);
        Assert.True(fullNameHeader >= 0 && fullNameHeader < emailHeader && emailHeader < organisationHeader, "The Email header is not between Full name and Organisation.");
        Assert.Contains("SortColumn=EmailAddress", body, StringComparison.Ordinal);

        var fullNameCell = body.IndexOf(">Jo Smith</td>", StringComparison.Ordinal);
        var emailCell = body.IndexOf(">jo.smith@example.test</td>", StringComparison.Ordinal);
        var organisationCell = body.IndexOf(">Acme Ltd</td>", StringComparison.Ordinal);
        Assert.True(fullNameCell >= 0 && fullNameCell < emailCell && emailCell < organisationCell, "The email is not between the full name and the organisation.");
    }

    [Fact]
    public async Task ExternalUsers_OffersSendInstructionsUntilTheyAreSent_ThenShowsSent()
    {
        var unsentId = Guid.NewGuid();
        var users = new PagedResult<MaintainedExternalUserDto>
        {
            Items =
            [
                new MaintainedExternalUserDto { Id = unsentId, UserName = "new.user@example.test", FullName = "New User", LoginInstructionsSent = false },
                new MaintainedExternalUserDto { Id = Guid.NewGuid(), UserName = "old.user@example.test", FullName = "Old User", LoginInstructionsSent = true }
            ],
            PageNumber = 1,
            PageSize = 10,
            TotalRecords = 2
        };

        using var factory = new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IExternalUsersApiService>();
                services.AddSingleton<IExternalUsersApiService>(new FakeExternalUsersApiService(users));
            }));
        var client = await factory.SignedInClientAsync();

        var body = await client.GetStringAsync("/UserAdmin/ExternalUsers");

        Assert.Contains(">Login instructions</th>", body, StringComparison.Ordinal);
        Assert.Equal(1, CountOf(body, "Send instructions<span"));
        Assert.Contains($"name=\"UserId\" value=\"{unsentId}\"", body, StringComparison.Ordinal);
        Assert.Contains("handler=SendInstructions", body, StringComparison.Ordinal);
        Assert.Equal(1, CountOf(body, ">Sent</strong>"));
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    private sealed class FakeExternalUsersApiService(PagedResult<MaintainedExternalUserDto> users) : IExternalUsersApiService
    {
        public Task<PagedResult<MaintainedExternalUserDto>> GetExternalUsersAsync(
            int pageNumber, int pageSize, ExternalUserSortColumn sortColumn, bool sortDescending, CancellationToken cancellationToken = default) =>
            Task.FromResult(users);

        public Task<ExternalUserAccessResult> PreRegisterExternalUserAsync(PreRegisterExternalUserContract request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> SetReviewEmailSubscriptionAsync(Guid id, bool subscribed, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ExternalUserProfileAccessContract?> GetProfileAccessAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ExternalUserAccessResult> SaveProfileAccessAsync(
            Guid id, Guid profileId, SaveExternalUserProfileAccessContract request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> DeleteProfileAccessAsync(Guid id, Guid profileId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ExternalUserAccessResult> SendLoginInstructionsAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
