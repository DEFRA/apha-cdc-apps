using System.Data;
using CDC.Api.Domain.Entities;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.Users;

public class UserRepositoryTests : IDisposable
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CidmSsoId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SsoUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static readonly string[] UserColumns =
    [
        "Id", "UserName", "FullName", "Organisation", "EmailAddress", "CidmSsoId", "SsoUserId",
        "IsProfileEditor", "IsPolicyProfileUser"
    ];

    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<UserRepository>> logger = new();

    public UserRepositoryTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private UserRepository CreateRepository() => new(new StubConnectionFactory(connection), logger.Object);

    [Fact]
    public async Task GetByCidmSsoIdAsync_MapsRow_WhenFound()
    {
        connection.Script(UserStoredProcedures.GetByCidmSsoId, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(UserColumns,
                [
                    [UserId, "user@example.com", "Jane External", "ACME Ltd", "user@example.com", CidmSsoId, SsoUserId, false, false]
                ])
            ]
        });

        var user = await CreateRepository().GetByCidmSsoIdAsync(CidmSsoId, CancellationToken.None);

        user.Should().NotBeNull();
        user!.Id.Should().Be(UserId);
        user.FullName.Should().Be("Jane External");
        user.CidmSsoId.Should().Be(CidmSsoId);
        user.SsoUserId.Should().Be(SsoUserId);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(UserStoredProcedures.GetByCidmSsoId);
        executed.CommandType.Should().Be(CommandType.StoredProcedure);
        executed.Parameters.Should().ContainKey("CidmSsoId").WhoseValue.Should().Be(CidmSsoId);
    }

    [Fact]
    public async Task GetByCidmSsoIdAsync_ReturnsNull_WhenNotFound()
    {
        connection.Script(UserStoredProcedures.GetByCidmSsoId, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty(UserColumns)]
        });

        var user = await CreateRepository().GetByCidmSsoIdAsync(CidmSsoId, CancellationToken.None);

        user.Should().BeNull();
    }

    [Fact]
    public async Task GetByCidmSsoIdAsync_LogsAndRethrows_WhenTheProcedureFails()
    {
        connection.Script(UserStoredProcedures.GetByCidmSsoId, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = async () => await CreateRepository().GetByCidmSsoIdAsync(CidmSsoId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetByEmailAddressAsync_MapsRow_WhenFound()
    {
        connection.Script(UserStoredProcedures.GetByEmailAddress, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(UserColumns,
                [
                    [UserId, "user@example.com", "Jane External", "ACME Ltd", "user@example.com", null, SsoUserId, false, false]
                ])
            ]
        });

        var user = await CreateRepository().GetByEmailAddressAsync("user@example.com", CancellationToken.None);

        user.Should().NotBeNull();
        user!.CidmSsoId.Should().Be(Guid.Empty);
        user.SsoUserId.Should().Be(SsoUserId);
    }

    [Fact]
    public async Task GetByEmailAddressAsync_ReturnsNull_WhenNotFound()
    {
        connection.Script(UserStoredProcedures.GetByEmailAddress, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty(UserColumns)]
        });

        var user = await CreateRepository().GetByEmailAddressAsync("nobody@example.com", CancellationToken.None);

        user.Should().BeNull();
    }

    [Fact]
    public async Task GetByEmailAddressAsync_LogsAndRethrows_WhenTheProcedureFails()
    {
        connection.Script(UserStoredProcedures.GetByEmailAddress, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = async () => await CreateRepository().GetByEmailAddressAsync("user@example.com", CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task UpdateCidmSsoIdAsync_ExecutesWithExpectedParameters()
    {
        connection.Script(UserStoredProcedures.UpdateCidmSsoId, new FakeCommandScript());

        await CreateRepository().UpdateCidmSsoIdAsync(UserId, CidmSsoId, CancellationToken.None);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(UserStoredProcedures.UpdateCidmSsoId);
        executed.Parameters.Should().ContainKey("Id").WhoseValue.Should().Be(UserId);
        executed.Parameters.Should().ContainKey("CidmSsoId").WhoseValue.Should().Be(CidmSsoId);
    }

    [Fact]
    public async Task UpdateCidmSsoIdAsync_LogsAndRethrows_WhenTheProcedureFails()
    {
        connection.Script(UserStoredProcedures.UpdateCidmSsoId, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = async () => await CreateRepository().UpdateCidmSsoIdAsync(UserId, CidmSsoId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task CreateExternalUserAsync_ExecutesWithExpectedParameters_AndReturnsTheSameEntity()
    {
        connection.Script(UserStoredProcedures.CreateExternalUser, new FakeCommandScript());

        var newUser = new ExternalUser
        {
            Id = UserId,
            UserName = "new.user@example.com",
            FullName = "New User",
            Organisation = "ACME Ltd",
            EmailAddress = "new.user@example.com",
            CidmSsoId = CidmSsoId,
            SsoUserId = null,
            IsProfileEditor = false,
            IsPolicyProfileUser = false
        };

        var created = await CreateRepository().CreateExternalUserAsync(newUser, CancellationToken.None);

        created.Should().BeSameAs(newUser);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(UserStoredProcedures.CreateExternalUser);
        executed.Parameters.Should().ContainKey("Id").WhoseValue.Should().Be(UserId);
        executed.Parameters.Should().ContainKey("UserName").WhoseValue.Should().Be("new.user@example.com");
        executed.Parameters.Should().ContainKey("CidmSsoId").WhoseValue.Should().Be(CidmSsoId);
    }

    [Fact]
    public async Task CreateExternalUserAsync_LogsAndRethrows_WhenTheProcedureFails()
    {
        connection.Script(UserStoredProcedures.CreateExternalUser, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var newUser = new ExternalUser
        {
            Id = UserId,
            UserName = "new.user@example.com",
            FullName = "New User",
            Organisation = "ACME Ltd",
            EmailAddress = "new.user@example.com",
            CidmSsoId = CidmSsoId,
            SsoUserId = null,
            IsProfileEditor = false,
            IsPolicyProfileUser = false
        };

        var act = async () => await CreateRepository().CreateExternalUserAsync(newUser, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task CreateExternalUserAsync_Throws_WhenNewUserIsNull()
    {
        var act = async () => await CreateRepository().CreateExternalUserAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetByCidmSsoIdAsync_Throws_WhenConnectionFactoryDoesNotReturnADbConnection()
    {
        var repository = new UserRepository(new NonDbConnectionFactory(), logger.Object);

        var act = async () => await repository.GetByCidmSsoIdAsync(CidmSsoId, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }

    private sealed class NonDbConnectionFactory : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => new Mock<IDbConnection>().Object;
    }
}
