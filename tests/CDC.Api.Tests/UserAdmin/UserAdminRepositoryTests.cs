using System.Data;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Api.Tests.UserAdmin;

public class UserAdminRepositoryTests : IDisposable
{
    private const string SelectUserById =
        "SELECT [Id], [UserName], [FullName], [Organisation], [EmailAddress], [SubscribedToReviewEmails], " +
        "[IsProfileEditor], [IsPolicyProfileUser], [SsoUserId], [LastUpdated] FROM [dbo].[User] WHERE [Id] = @UserId";

    private static readonly Guid UserId = Guid.Parse("6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f");
    private static readonly Guid SsoUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly byte[] NewRowVersion = [1, 2, 3, 4, 5, 6, 7, 9];

    private static readonly string[] UserColumns =
    [
        "Id", "UserName", "FullName", "Organisation", "EmailAddress", "SubscribedToReviewEmails",
        "IsProfileEditor", "IsPolicyProfileUser", "SsoUserId", "LastUpdated"
    ];

    private readonly FakeDbConnection connection = new();

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task GetGlobalUsersAsync_MapsTheSubscriptionFlag()
    {
        connection.Script(UserAdminStoredProcedures.GetAllGlobalUsers, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "UserName", "FullName", "Organisation", "SubscribedToReviewEmails", "IsProfileEditor", "IsPolicyProfileUser", "LastUpdated"],
                    [[UserId, "jbloggs", "Joe Bloggs", "APHA", true, true, false, RowVersion]])
            ]
        });

        var users = await CreateRepository().GetGlobalUsersAsync(CancellationToken.None);

        var user = users.Should().ContainSingle().Subject;
        user.Id.Should().Be(UserId);
        user.SubscribedToReviewEmails.Should().BeTrue();
        user.IsExternal.Should().BeFalse();
    }

    [Fact]
    public async Task GetExternalUsersAsync_MapsTheSubscriptionFlagAndEmailAddress()
    {
        connection.Script(UserAdminStoredProcedures.GetAllSsoUserMaintenance, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "UserName", "FullName", "EmailAddress", "Organisation", "SubscribedToReviewEmails", "SsoUserId", "LastUpdated"],
                    [[UserId, "jbloggs", "Joe Bloggs", "joe.bloggs@example.gov.uk", "APHA", false, SsoUserId, RowVersion]])
            ]
        });

        var users = await CreateRepository().GetExternalUsersAsync(CancellationToken.None);

        var user = users.Should().ContainSingle().Subject;
        user.SubscribedToReviewEmails.Should().BeFalse();
        user.EmailAddress.Should().Be("joe.bloggs@example.gov.uk");
        user.IsExternal.Should().BeTrue();
    }

    [Fact]
    public async Task GetUserAsync_ReturnsNull_WhenNoSuchUserExists()
    {
        ScriptUserLookup(rows: []);

        var user = await CreateRepository().GetUserAsync(UserId, CancellationToken.None);

        user.Should().BeNull();
    }

    [Fact]
    public async Task UpdateReviewEmailSubscriptionAsync_ReturnsNull_WhenNoSuchUserExists()
    {
        ScriptUserLookup(rows: []);

        var lastUpdated = await CreateRepository()
            .UpdateReviewEmailSubscriptionAsync(UserId, true, RowVersion, CancellationToken.None);

        lastUpdated.Should().BeNull();
    }

    [Fact]
    public async Task UpdateReviewEmailSubscriptionAsync_PreservesEveryOtherAttributeOfAGlobalUser()
    {
        ScriptUserLookup([GlobalUserRow(subscribed: false)]);
        connection.Script(UserAdminStoredProcedures.UpsertGlobalUser, new FakeCommandScript
        {
            OutputValues = new Dictionary<string, object?> { ["@NewLastUpdated"] = NewRowVersion }
        });

        var lastUpdated = await CreateRepository()
            .UpdateReviewEmailSubscriptionAsync(UserId, true, RowVersion, CancellationToken.None);

        lastUpdated.Should().BeEquivalentTo(NewRowVersion);

        var upsert = connection.Executed.Should()
            .ContainSingle(command => command.CommandText == UserAdminStoredProcedures.UpsertGlobalUser).Subject;
        upsert.CommandType.Should().Be(CommandType.StoredProcedure);
        ParameterValue(upsert, "@SubscribedToReviewEmails").Should().Be(true);
        ParameterValue(upsert, "@UserName").Should().Be("jbloggs");
        ParameterValue(upsert, "@IsProfileEditor").Should().Be(true);
        ParameterValue(upsert, "@IsPolicyProfileUser").Should().Be(false);
        ParameterValue(upsert, "@LastUpdated").Should().BeEquivalentTo(RowVersion);
    }

    [Fact]
    public async Task UpdateReviewEmailSubscriptionAsync_UpsertsAnExternalUserAndRereadsTheRowVersion()
    {
        ScriptUserLookup([ExternalUserRow(subscribed: true, RowVersion)]);
        connection.Script(UserAdminStoredProcedures.UpsertSsoUser, new FakeCommandScript());

        var lastUpdated = await CreateRepository()
            .UpdateReviewEmailSubscriptionAsync(UserId, false, RowVersion, CancellationToken.None);

        // spiSsoUser has no row version output, so the repository re-reads the user afterwards.
        lastUpdated.Should().BeEquivalentTo(RowVersion);
        connection.Executed.Count(command => command.CommandText == SelectUserById).Should().Be(2);

        var upsert = connection.Executed.Should()
            .ContainSingle(command => command.CommandText == UserAdminStoredProcedures.UpsertSsoUser).Subject;
        ParameterValue(upsert, "@SubscribedToEmails").Should().Be(false);
        ParameterValue(upsert, "@SsoUserId").Should().Be(SsoUserId);
        ParameterValue(upsert, "@EmailAddress").Should().Be("joe.bloggs@example.gov.uk");
    }

    [Fact]
    public async Task UpdateReviewEmailSubscriptionAsync_ThrowsConcurrencyException_WhenTheRowVersionHasMovedOn()
    {
        ScriptUserLookup([GlobalUserRow(subscribed: false)]);
        connection.Script(UserAdminStoredProcedures.UpsertGlobalUser, new FakeCommandScript
        {
            Throws = new FakeDbException("The User information has been edited by another user")
        });

        var act = async () => await CreateRepository()
            .UpdateReviewEmailSubscriptionAsync(UserId, true, RowVersion, CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    [Fact]
    public async Task UpdateReviewEmailSubscriptionAsync_RethrowsOtherDatabaseFailures()
    {
        ScriptUserLookup([GlobalUserRow(subscribed: false)]);
        connection.Script(UserAdminStoredProcedures.UpsertGlobalUser, new FakeCommandScript
        {
            Throws = new FakeDbException("Deadlock victim")
        });

        var act = async () => await CreateRepository()
            .UpdateReviewEmailSubscriptionAsync(UserId, true, RowVersion, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task UpdateReviewEmailSubscriptionAsync_RethrowsWhenTheExternalUpsertFails()
    {
        ScriptUserLookup([ExternalUserRow(subscribed: true, RowVersion)]);
        connection.Script(UserAdminStoredProcedures.UpsertSsoUser, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = async () => await CreateRepository()
            .UpdateReviewEmailSubscriptionAsync(UserId, false, RowVersion, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetGlobalUsersAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(UserAdminStoredProcedures.GetAllGlobalUsers, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = async () => await CreateRepository().GetGlobalUsersAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetExternalUsersAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(UserAdminStoredProcedures.GetAllSsoUserMaintenance, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = async () => await CreateRepository().GetExternalUsersAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetUserAsync_RethrowsAndLogs_OnDbException()
    {
        connection.Script(SelectUserById, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = async () => await CreateRepository().GetUserAsync(UserId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    private void ScriptUserLookup(IReadOnlyList<object?[]> rows) =>
        connection.Script(SelectUserById, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(UserColumns, rows)]
        });

    private static object?[] GlobalUserRow(bool subscribed) =>
        [UserId, "jbloggs", "Joe Bloggs", "APHA", null, subscribed, true, false, null, RowVersion];

    private static object?[] ExternalUserRow(bool subscribed, byte[] rowVersion) =>
        [UserId, "jbloggs", "Joe Bloggs", "APHA", "joe.bloggs@example.gov.uk", subscribed, false, false, SsoUserId, rowVersion];

    private static object? ParameterValue(RecordedCommand command, string name) =>
        command.Parameters.TryGetValue(name, out var value)
            ? value
            : command.Parameters[name.TrimStart('@')];

    private UserAdminRepository CreateRepository() =>
        new(new StubConnectionFactory(connection), NullLogger<UserAdminRepository>.Instance);

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }
}
