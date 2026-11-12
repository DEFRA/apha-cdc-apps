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
    private static readonly string[] UserColumns = ["Id", "UserName", "FullName", "EmailAddress", "Organisation", "SubscribedToReviewEmails"];
    private static readonly string[] ProfileColumns = ["Id", "Title"];
    private static readonly string[] GrantColumns = ["UserId", "ProfileId", "Title", "RoleId", "RoleName", "IsContributor"];
    private static readonly string[] SectionColumns = ["UserId", "ProfileId", "SectionId"];
    private static readonly string[] LoginRecipientColumns = ["Id", "FullName", "EmailAddress", "LoginInstructionsSent"];

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
        SubscribedToReviewEmails = true
    };

    [Fact]
    public async Task GetExternalUsersAsync_MapsEveryUserRow()
    {
        var id = Guid.NewGuid();
        connection.Script("spgaExternalUserMaintenance", new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(UserColumns, [[id, "jsmith", "Jo Smith", "jo@example.test", "APHA", true]])]
        });

        var users = await CreateRepository().GetExternalUsersAsync(CancellationToken.None);

        var user = users.Should().ContainSingle().Subject;
        user.Id.Should().Be(id);
        user.UserName.Should().Be("jsmith");
        user.FullName.Should().Be("Jo Smith");
        user.EmailAddress.Should().Be("jo@example.test");
        user.Organisation.Should().Be("APHA");
        user.SubscribedToReviewEmails.Should().BeTrue();
        connection.Executed.Should().ContainSingle().Which.CommandType.Should().Be(CommandType.StoredProcedure);
    }

    [Fact]
    public async Task GetExternalUsersAsync_Throws_WhenTheDatabaseFails()
    {
        connection.Script("spgaExternalUserMaintenance", new FakeCommandScript { Throws = new FakeDbException("boom") });

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
        connection.Script("spgaExternalUserMaintenance", new FakeCommandScript
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
        connection.Script("spgaExternalUserMaintenance", new FakeCommandScript
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
        connection.Script("spgaExternalUserMaintenance", new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetProfileAccessAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task SaveExternalUserAsync_SavesTheSubscriptionByUserId()
    {
        connection.Script("spuExternalUserSubscribedToReviewEmails", new FakeCommandScript());
        var user = User();

        await CreateRepository().SaveExternalUserAsync(user, CancellationToken.None);

        var recorded = connection.Executed.Should().ContainSingle().Subject;
        recorded.CommandType.Should().Be(CommandType.StoredProcedure);
        recorded.Parameters["Id"].Should().Be(user.Id);
        recorded.Parameters["SubscribedToReviewEmails"].Should().Be(true);
        recorded.Parameters.Should().HaveCount(2);
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
        connection.Script("spuExternalUserSubscribedToReviewEmails", new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().SaveExternalUserAsync(User(), CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task PreRegisterExternalUserAsync_PassesEveryParameter_AndReturnsTrueWhenCreated()
    {
        connection.Script("spiExternalUserPreRegistration", new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["Created"], [[true]])]
        });
        var id = Guid.NewGuid();

        var created = await CreateRepository().PreRegisterExternalUserAsync(
            id, "jane@example.test", "Jane Doe", "Org", "jane@example.test", CancellationToken.None);

        created.Should().BeTrue();
        var recorded = connection.Executed.Should().ContainSingle().Subject;
        recorded.CommandType.Should().Be(CommandType.StoredProcedure);
        recorded.Parameters["Id"].Should().Be(id);
        recorded.Parameters["UserName"].Should().Be("jane@example.test");
        recorded.Parameters["FullName"].Should().Be("Jane Doe");
        recorded.Parameters["Organisation"].Should().Be("Org");
        recorded.Parameters["EmailAddress"].Should().Be("jane@example.test");
    }

    [Fact]
    public async Task PreRegisterExternalUserAsync_ReturnsFalse_WhenTheEmailOrUserNameIsAlreadyUsed()
    {
        connection.Script("spiExternalUserPreRegistration", new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["Created"], [[false]])]
        });

        var created = await CreateRepository().PreRegisterExternalUserAsync(
            Guid.NewGuid(), "jane@example.test", "Jane Doe", "Org", "jane@example.test", CancellationToken.None);

        created.Should().BeFalse();
    }

    [Fact]
    public async Task PreRegisterExternalUserAsync_Rethrows_WhenTheDatabaseFails()
    {
        connection.Script("spiExternalUserPreRegistration", new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().PreRegisterExternalUserAsync(
            Guid.NewGuid(), "jane@example.test", "Jane Doe", "Org", "jane@example.test", CancellationToken.None);

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

    [Fact]
    public async Task GetUsersAwaitingLoginInstructionsAsync_ReturnsTheIdsOfTheUsersStillToBeSent()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        connection.Script("spgaExternalUsersAwaitingLoginInstructions", new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["Id"], [[first], [second]])]
        });

        var ids = await CreateRepository().GetUsersAwaitingLoginInstructionsAsync(CancellationToken.None);

        ids.Should().BeEquivalentTo([first, second]);
        connection.Executed.Should().ContainSingle().Which.CommandType.Should().Be(CommandType.StoredProcedure);
    }

    [Fact]
    public async Task GetUsersAwaitingLoginInstructionsAsync_ReturnsNothing_WhenEveryoneHasBeenSent()
    {
        connection.Script("spgaExternalUsersAwaitingLoginInstructions", new FakeCommandScript { ResultSets = [new FakeResultSet(["Id"], [])] });

        (await CreateRepository().GetUsersAwaitingLoginInstructionsAsync(CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task GetUsersAwaitingLoginInstructionsAsync_Rethrows_WhenTheDatabaseFails()
    {
        connection.Script("spgaExternalUsersAwaitingLoginInstructions", new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetUsersAwaitingLoginInstructionsAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetLoginRecipientAsync_MapsTheRow_AndPassesTheUserId()
    {
        var id = Guid.NewGuid();
        connection.Script("spgExternalUserLoginInstructions", new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(LoginRecipientColumns, [[id, "Jo Smith", "jo@example.test", false]])]
        });

        var recipient = await CreateRepository().GetLoginRecipientAsync(id, CancellationToken.None);

        recipient.Should().Be(new ExternalUserLoginRecipient(id, "Jo Smith", "jo@example.test", LoginInstructionsSent: false));
        connection.Executed.Should().ContainSingle().Which.Parameters["Id"].Should().Be(id);
    }

    [Fact]
    public async Task GetLoginRecipientAsync_ReturnsNull_WhenTheUserIsNotAnExternalUser()
    {
        connection.Script("spgExternalUserLoginInstructions", new FakeCommandScript { ResultSets = [new FakeResultSet(LoginRecipientColumns, [])] });

        (await CreateRepository().GetLoginRecipientAsync(Guid.NewGuid(), CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public async Task GetLoginRecipientAsync_Rethrows_WhenTheDatabaseFails()
    {
        connection.Script("spgExternalUserLoginInstructions", new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetLoginRecipientAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MarkLoginInstructionsSentAsync_PassesEveryParameter_AndReturnsWhetherItWasRecorded(bool recorded)
    {
        connection.Script("spuExternalUserLoginInstructionsSent", new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["Recorded"], [[recorded]])]
        });
        var id = Guid.NewGuid();
        var sentDate = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);

        var result = await CreateRepository().MarkLoginInstructionsSentAsync(id, "jo@example.test", sentDate, CancellationToken.None);

        result.Should().Be(recorded);
        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandType.Should().Be(CommandType.StoredProcedure);
        executed.Parameters["Id"].Should().Be(id);
        executed.Parameters["EmailAddress"].Should().Be("jo@example.test");
        executed.Parameters["SentDate"].Should().Be(sentDate);
    }

    [Fact]
    public async Task MarkLoginInstructionsSentAsync_Rethrows_WhenTheDatabaseFails()
    {
        connection.Script("spuExternalUserLoginInstructionsSent", new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().MarkLoginInstructionsSentAsync(Guid.NewGuid(), "jo@example.test", DateTime.UtcNow, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }
}
