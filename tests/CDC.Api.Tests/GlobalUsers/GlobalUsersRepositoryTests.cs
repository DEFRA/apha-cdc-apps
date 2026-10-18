using System.Data;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using CDC.Common.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.GlobalUsers;

public class GlobalUsersRepositoryTests : IDisposable
{
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<GlobalUsersRepository>> logger = new();

    public GlobalUsersRepositoryTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private GlobalUsersRepository CreateRepository() => new(new StubConnectionFactory(connection), logger.Object);

    private static GlobalUser User(GlobalUserRole role = GlobalUserRole.ProfileEditor) => new()
    {
        Id = Guid.NewGuid(),
        UserName = @"DEFRA\jsmith",
        FullName = "Jo Smith",
        Organisation = "APHA",
        SubscribedToReviewEmails = true,
        Role = role,
        LastUpdated = RowVersion
    };

    [Fact]
    public async Task GetGlobalUsersAsync_MapsRowsAndRoles()
    {
        var editorId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        connection.Script("spgaGlobalUser", new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "UserName", "FullName", "Organisation", "SubscribedToReviewEmails", "IsProfileEditor", "IsPolicyProfileUser", "LastUpdated"],
                    [
                        [editorId, @"D\a", "A", "APHA", true, true, false, RowVersion],
                        [policyId, @"D\b", "B", "Defra", false, false, true, RowVersion]
                    ])
            ]
        });

        var users = await CreateRepository().GetGlobalUsersAsync(CancellationToken.None);

        users.Should().HaveCount(2);
        users[0].Id.Should().Be(editorId);
        users[0].Role.Should().Be(GlobalUserRole.ProfileEditor);
        users[0].SubscribedToReviewEmails.Should().BeTrue();
        users[1].Role.Should().Be(GlobalUserRole.PolicyProfileUser);
        users[1].LastUpdated.Should().Equal(RowVersion);
        connection.Executed.Should().ContainSingle().Which.CommandType.Should().Be(CommandType.StoredProcedure);
    }

    [Fact]
    public async Task GetGlobalUsersAsync_Throws_WhenTheDatabaseFails()
    {
        connection.Script("spgaGlobalUser", new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetGlobalUsersAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetUserAsync_MapsRow_AndFlagsSsoUsers()
    {
        var id = Guid.NewGuid();
        connection.Script("spgUser", new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "UserName", "FullName", "Organisation", "SubscribedToReviewEmails", "IsProfileEditor", "IsPolicyProfileUser", "SsoUserId", "LastUpdated"],
                    [[id, @"D\a", "A", "APHA", false, false, true, Guid.NewGuid(), RowVersion]])
            ]
        });

        var user = await CreateRepository().GetUserAsync(id, CancellationToken.None);

        user.Should().NotBeNull();
        user!.Role.Should().Be(GlobalUserRole.PolicyProfileUser);
        user.IsSsoUser.Should().BeTrue();
        connection.Executed.Single().Parameters.Should().ContainKey("UserId").WhoseValue.Should().Be(id);
    }

    [Fact]
    public async Task GetUserAsync_ReturnsNull_WhenNoRow()
    {
        connection.Script("spgUser", new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty(
                ["Id", "UserName", "FullName", "Organisation", "SubscribedToReviewEmails", "IsProfileEditor", "IsPolicyProfileUser", "SsoUserId", "LastUpdated"],
                [typeof(Guid), typeof(string), typeof(string), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(Guid), typeof(byte[])])]
        });

        var user = await CreateRepository().GetUserAsync(Guid.NewGuid(), CancellationToken.None);

        user.Should().BeNull();
    }

    [Fact]
    public async Task GetUserAsync_Throws_WhenTheDatabaseFails()
    {
        connection.Script("spgUser", new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetUserAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task FindUserByUsernameAsync_ReturnsMatch()
    {
        var id = Guid.NewGuid();
        connection.Script("spgUserAuthorisation", new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "FullName", "Organisation", "IsProfileEditor", "IsPolicyProfileUser", "UserName", "SsoUserId", "IsUserManagementSystem"],
                    [[id, "A", "APHA", true, false, @"D\a", Guid.Empty, true]])
            ]
        });

        var match = await CreateRepository().FindUserByUsernameAsync(@"D\a", CancellationToken.None);

        match.Should().NotBeNull();
        match!.Id.Should().Be(id);
        match.IsUserManagementSystem.Should().BeTrue();
    }

    [Fact]
    public async Task FindUserByUsernameAsync_ReturnsNull_WhenNoRow()
    {
        connection.Script("spgUserAuthorisation", new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty(
                ["Id", "FullName", "Organisation", "IsProfileEditor", "IsPolicyProfileUser", "UserName", "SsoUserId", "IsUserManagementSystem"],
                [typeof(Guid), typeof(string), typeof(string), typeof(bool), typeof(bool), typeof(string), typeof(Guid), typeof(bool)])]
        });

        var match = await CreateRepository().FindUserByUsernameAsync(@"D\a", CancellationToken.None);

        match.Should().BeNull();
    }

    [Fact]
    public async Task FindUserByUsernameAsync_Throws_WhenTheDatabaseFails()
    {
        connection.Script("spgUserAuthorisation", new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().FindUserByUsernameAsync(@"D\a", CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task SaveGlobalUserAsync_PassesRoleFlagsAndRowVersion()
    {
        connection.Script("spiGlobalUser", new FakeCommandScript());
        var user = User(GlobalUserRole.PolicyProfileUser);

        await CreateRepository().SaveGlobalUserAsync(user, CancellationToken.None);

        var recorded = connection.Executed.Should().ContainSingle().Subject;
        recorded.Parameters["UserId"].Should().Be(user.Id);
        recorded.Parameters["IsProfileEditor"].Should().Be(false);
        recorded.Parameters["IsPolicyProfileUser"].Should().Be(true);
        recorded.Parameters["SubscribedToReviewEmails"].Should().Be(true);
    }

    [Fact]
    public async Task SaveGlobalUserAsync_ThrowsConcurrencyException_WhenRowVersionMoved()
    {
        connection.Script("spiGlobalUser", new FakeCommandScript { Throws = new FakeDbException("The User information has been edited by another user") });

        var act = () => CreateRepository().SaveGlobalUserAsync(User(), CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    [Fact]
    public async Task SaveGlobalUserAsync_ThrowsDuplicateUsernameException_WhenUsernameInUse()
    {
        connection.Script("spiGlobalUser", new FakeCommandScript { Throws = new FakeDbException("There is already a user with the specified username") });

        var act = () => CreateRepository().SaveGlobalUserAsync(User(), CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateUsernameException>();
    }

    [Fact]
    public async Task SaveGlobalUserAsync_Rethrows_OtherDatabaseFailures()
    {
        connection.Script("spiGlobalUser", new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().SaveGlobalUserAsync(User(), CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task DeleteGlobalUserAsync_ExecutesProcedure()
    {
        connection.Script("spdGlobalUser", new FakeCommandScript());
        var id = Guid.NewGuid();

        await CreateRepository().DeleteGlobalUserAsync(id, RowVersion, CancellationToken.None);

        connection.Executed.Should().ContainSingle().Which.Parameters["UserId"].Should().Be(id);
    }

    [Fact]
    public async Task DeleteGlobalUserAsync_ThrowsConcurrencyException_WhenRowVersionMoved()
    {
        connection.Script("spdGlobalUser", new FakeCommandScript { Throws = new FakeDbException("The User information has been edited by another user") });

        var act = () => CreateRepository().DeleteGlobalUserAsync(Guid.NewGuid(), RowVersion, CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    [Fact]
    public async Task DeleteGlobalUserAsync_Rethrows_OtherDatabaseFailures()
    {
        connection.Script("spdGlobalUser", new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().DeleteGlobalUserAsync(Guid.NewGuid(), RowVersion, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetUserAsync_Rethrows_WhenTheDatabaseFails()
    {
        connection.Script("spgUser", new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetUserAsync(Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task FindUserByUsernameAsync_Rethrows_WhenTheDatabaseFails()
    {
        connection.Script("spgUserAuthorisation", new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().FindUserByUsernameAsync(@"DEFRA\jsmith", CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetGlobalUsersAsync_Rethrows_WhenTheConnectionCannotBeOpened()
    {
        connection.ThrowOnOpen = new FakeDbException("cannot open");

        var act = () => CreateRepository().GetGlobalUsersAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetGlobalUsersAsync_Throws_WhenTheFactoryDoesNotReturnADbConnection()
    {
        var nonDbConnection = new Mock<IDbConnection>();
        var factory = new Mock<IDbConnectionFactory>();
        factory.Setup(f => f.CreateConnection()).Returns(nonDbConnection.Object);
        var repository = new GlobalUsersRepository(factory.Object, logger.Object);

        var act = () => repository.GetGlobalUsersAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }
}
