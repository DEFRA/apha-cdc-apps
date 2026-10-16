using System.Data;
using CDC.Api.Domain.Entities;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.ExternalUsers;

public class ExternalUsersRepositoryTests : IDisposable
{
    private static readonly string[] UserColumns = ["Id", "UserName", "FullName", "EmailAddress", "Organisation", "SubscribedToReviewEmails", "SsoUserId"];
    private static readonly string[] ProfileColumns = ["Id", "Title"];
    private static readonly string[] GrantColumns = ["UserId", "ProfileId", "Title", "RoleId", "RoleName", "IsContributor"];
    private static readonly string[] SectionColumns = ["UserId", "ProfileId", "SectionId"];

    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<ExternalUsersRepository>> logger = new();

    public ExternalUsersRepositoryTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private ExternalUsersRepository CreateRepository() => new(new StubConnectionFactory(connection), logger.Object);

    private static ExternalUserAccount User() => new()
    {
        Id = Guid.NewGuid(),
        UserName = "jsmith",
        FullName = "Jo Smith",
        EmailAddress = "jo@example.test",
        Organisation = "APHA",
        SubscribedToReviewEmails = true,
        SsoUserId = Guid.NewGuid()
    };

    [Fact]
    public async Task GetExternalUsersAsync_MapsEveryUserRow()
    {
        var id = Guid.NewGuid();
        var ssoId = Guid.NewGuid();
        connection.Script("spgaSsoUserMaintenance", new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(UserColumns, [[id, "jsmith", "Jo Smith", "jo@example.test", "APHA", true, ssoId]])]
        });

        var users = await CreateRepository().GetExternalUsersAsync(CancellationToken.None);

        var user = users.Should().ContainSingle().Subject;
        user.Id.Should().Be(id);
        user.UserName.Should().Be("jsmith");
        user.FullName.Should().Be("Jo Smith");
        user.EmailAddress.Should().Be("jo@example.test");
        user.Organisation.Should().Be("APHA");
        user.SubscribedToReviewEmails.Should().BeTrue();
        user.SsoUserId.Should().Be(ssoId);
        connection.Executed.Should().ContainSingle().Which.CommandType.Should().Be(CommandType.StoredProcedure);
    }

    [Fact]
    public async Task GetExternalUsersAsync_Throws_WhenTheDatabaseFails()
    {
        connection.Script("spgaSsoUserMaintenance", new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetExternalUsersAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetProfileAccessAsync_SplitsGrantedAndAvailableProfiles_ForTheRequestedUser()
    {
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var grantedId = Guid.NewGuid();
        var availableId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        connection.Script("spgaSsoUserMaintenance", new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(UserColumns, []),
                new FakeResultSet(ProfileColumns, [[grantedId, "Anthrax"], [availableId, null]]),
                new FakeResultSet(
                    GrantColumns,
                    [
                        [userId, grantedId, "Anthrax", roleId, "Profile author", true],
                        [otherUserId, availableId, "Other", roleId, "Profile author", true]
                    ]),
                new FakeResultSet(
                    SectionColumns,
                    [
                        [userId, grantedId, sectionId],
                        [otherUserId, grantedId, Guid.NewGuid()]
                    ])
            ]
        });

        var access = await CreateRepository().GetProfileAccessAsync(userId, CancellationToken.None);

        var grant = access.Profiles.Should().ContainSingle().Subject;
        grant.ProfileId.Should().Be(grantedId);
        grant.RoleId.Should().Be(roleId);
        grant.RoleName.Should().Be("Profile author");
        grant.IsContributor.Should().BeTrue();
        grant.SectionPermissionIds.Should().Equal(sectionId);
        var available = access.AvailableProfiles.Should().ContainSingle().Subject;
        available.Id.Should().Be(availableId);
        available.Title.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProfileAccessAsync_ReturnsNoGrants_WhenOnlyTheProfileResultSetIsReturned()
    {
        var profileId = Guid.NewGuid();
        connection.Script("spgaSsoUserMaintenance", new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(UserColumns, []),
                new FakeResultSet(ProfileColumns, [[profileId, "Anthrax"]])
            ]
        });

        var access = await CreateRepository().GetProfileAccessAsync(Guid.NewGuid(), CancellationToken.None);

        access.Profiles.Should().BeEmpty();
        access.AvailableProfiles.Should().ContainSingle().Which.Id.Should().Be(profileId);
    }

    [Fact]
    public async Task GetProfileAccessAsync_Throws_WhenTheDatabaseFails()
    {
        connection.Script("spgaSsoUserMaintenance", new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetProfileAccessAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task SaveExternalUserAsync_PassesEveryParameter()
    {
        connection.Script("spiSsoUser", new FakeCommandScript());
        var user = User();

        await CreateRepository().SaveExternalUserAsync(user, CancellationToken.None);

        var recorded = connection.Executed.Should().ContainSingle().Subject;
        recorded.Parameters["UserName"].Should().Be("jsmith");
        recorded.Parameters["FullName"].Should().Be("Jo Smith");
        recorded.Parameters["EmailAddress"].Should().Be("jo@example.test");
        recorded.Parameters["Organisation"].Should().Be("APHA");
        recorded.Parameters["SubscribedToEmails"].Should().Be(true);
        recorded.Parameters["SsoUserId"].Should().Be(user.SsoUserId);
    }

    [Fact]
    public async Task SaveExternalUserAsync_ThrowsArgumentNullException_WhenUserIsNull()
    {
        var act = () => CreateRepository().SaveExternalUserAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task SaveExternalUserAsync_Rethrows_WhenTheDatabaseFails()
    {
        connection.Script("spiSsoUser", new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().SaveExternalUserAsync(User(), CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetExternalUsersAsync_Rethrows_WhenTheConnectionCannotBeOpened()
    {
        connection.ThrowOnOpen = new FakeDbException("cannot open");

        var act = () => CreateRepository().GetExternalUsersAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetExternalUsersAsync_Throws_WhenTheFactoryDoesNotReturnADbConnection()
    {
        var nonDbConnection = new Mock<IDbConnection>();
        var factory = new Mock<IDbConnectionFactory>();
        factory.Setup(f => f.CreateConnection()).Returns(nonDbConnection.Object);
        var repository = new ExternalUsersRepository(factory.Object, logger.Object);

        var act = () => repository.GetExternalUsersAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }
}
