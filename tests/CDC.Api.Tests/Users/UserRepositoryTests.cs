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
    private static readonly Guid SsoUserIdExt = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SsoUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SsoUserIdInt = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly string[] UserColumns =
    [
        "Id", "UserName", "FullName", "Organisation", "EmailAddress", "SsoUserIdExt", "SsoUserId",
        "IsProfileEditor", "IsPolicyProfileUser"
    ];

    private static readonly string[] InternalUserColumns =
    [
        "Id", "UserName", "FullName", "IsProfileEditor", "IsPolicyProfileUser", "SsoUserIdInt"
    ];

    private static readonly string[] ActingUserColumns =
    [
        "Id", "IsProfileEditor", "IsPolicyProfileUser", "SsoUserIdInt", "SsoUserIdExt"
    ];

    private static readonly string[] ProfileRoleColumns = ["ProfileId", "RoleId", "RoleName", "IsContributor"];

    private static readonly string[] SectionAssignmentColumns = ["ProfileId", "ProfileSectionId"];

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
    public async Task GetBySsoUserIdExtAsync_MapsRow_WhenFound()
    {
        connection.Script(UserStoredProcedures.GetUserAuthorisation, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(UserColumns,
                [
                    [UserId, "user@example.com", "Jane External", "ACME Ltd", "user@example.com", SsoUserIdExt, SsoUserId, false, false]
                ])
            ]
        });

        var user = await CreateRepository().GetBySsoUserIdExtAsync(SsoUserIdExt, CancellationToken.None);

        user.Should().NotBeNull();
        user!.Id.Should().Be(UserId);
        user.FullName.Should().Be("Jane External");
        user.SsoUserIdExt.Should().Be(SsoUserIdExt);
        user.SsoUserId.Should().Be(SsoUserId);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(UserStoredProcedures.GetUserAuthorisation);
        executed.CommandType.Should().Be(CommandType.StoredProcedure);
        executed.Parameters.Should().ContainKey("SsoUserIdExt").WhoseValue.Should().Be(SsoUserIdExt);
    }

    [Fact]
    public async Task GetBySsoUserIdExtAsync_ReturnsNull_WhenNotFound()
    {
        connection.Script(UserStoredProcedures.GetUserAuthorisation, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty(UserColumns)]
        });

        var user = await CreateRepository().GetBySsoUserIdExtAsync(SsoUserIdExt, CancellationToken.None);

        user.Should().BeNull();
    }

    [Fact]
    public async Task GetBySsoUserIdExtAsync_LogsAndRethrows_WhenTheProcedureFails()
    {
        connection.Script(UserStoredProcedures.GetUserAuthorisation, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = async () => await CreateRepository().GetBySsoUserIdExtAsync(SsoUserIdExt, CancellationToken.None);

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
        user!.SsoUserIdExt.Should().Be(Guid.Empty);
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
    public async Task UpdateSsoUserIdExtAsync_ExecutesWithExpectedParameters()
    {
        connection.Script(UserStoredProcedures.UpdateSsoUserIdExt, new FakeCommandScript());

        await CreateRepository().UpdateSsoUserIdExtAsync(UserId, SsoUserIdExt, CancellationToken.None);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(UserStoredProcedures.UpdateSsoUserIdExt);
        executed.Parameters.Should().ContainKey("Id").WhoseValue.Should().Be(UserId);
        executed.Parameters.Should().ContainKey("SsoUserIdExt").WhoseValue.Should().Be(SsoUserIdExt);
    }

    [Fact]
    public async Task UpdateSsoUserIdExtAsync_LogsAndRethrows_WhenTheProcedureFails()
    {
        connection.Script(UserStoredProcedures.UpdateSsoUserIdExt, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = async () => await CreateRepository().UpdateSsoUserIdExtAsync(UserId, SsoUserIdExt, CancellationToken.None);

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
            SsoUserIdExt = SsoUserIdExt,
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
        executed.Parameters.Should().ContainKey("SsoUserIdExt").WhoseValue.Should().Be(SsoUserIdExt);
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
            SsoUserIdExt = SsoUserIdExt,
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
    public async Task GetBySsoUserIdExtAsync_Throws_WhenConnectionFactoryDoesNotReturnADbConnection()
    {
        var repository = new UserRepository(new NonDbConnectionFactory(), logger.Object);

        var act = async () => await repository.GetBySsoUserIdExtAsync(SsoUserIdExt, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task GetBySsoUserIdIntAsync_MapsRow_WhenFound()
    {
        connection.Script(UserStoredProcedures.GetUserAuthorisation, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(InternalUserColumns,
                [
                    [UserId, @"DEFRA\jdoe", "Jane Internal", false, false, SsoUserIdInt]
                ])
            ]
        });

        var user = await CreateRepository().GetBySsoUserIdIntAsync(SsoUserIdInt, CancellationToken.None);

        user.Should().NotBeNull();
        user!.Id.Should().Be(UserId);
        user.FullName.Should().Be("Jane Internal");
        user.SsoUserIdInt.Should().Be(SsoUserIdInt);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(UserStoredProcedures.GetUserAuthorisation);
        executed.CommandType.Should().Be(CommandType.StoredProcedure);
        executed.Parameters.Should().ContainKey("SsoUserIdInt").WhoseValue.Should().Be(SsoUserIdInt);
    }

    [Fact]
    public async Task GetBySsoUserIdIntAsync_ReturnsNull_WhenNotFound()
    {
        connection.Script(UserStoredProcedures.GetUserAuthorisation, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty(InternalUserColumns)]
        });

        var user = await CreateRepository().GetBySsoUserIdIntAsync(SsoUserIdInt, CancellationToken.None);

        user.Should().BeNull();
    }

    [Fact]
    public async Task GetBySsoUserIdIntAsync_LogsAndRethrows_WhenTheProcedureFails()
    {
        connection.Script(UserStoredProcedures.GetUserAuthorisation, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = async () => await CreateRepository().GetBySsoUserIdIntAsync(SsoUserIdInt, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetByUserNameAsync_MapsRow_WhenFound()
    {
        connection.Script(UserStoredProcedures.GetUserAuthorisation, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(InternalUserColumns,
                [
                    [UserId, @"DEFRA\jdoe", "Jane Internal", false, false, null]
                ])
            ]
        });

        var user = await CreateRepository().GetByUserNameAsync(@"DEFRA\jdoe", CancellationToken.None);

        user.Should().NotBeNull();
        user!.SsoUserIdInt.Should().BeNull();

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.Parameters.Should().ContainKey("UserName").WhoseValue.Should().Be(@"DEFRA\jdoe");
    }

    [Fact]
    public async Task GetByUserNameAsync_ReturnsNull_WhenNotFound()
    {
        connection.Script(UserStoredProcedures.GetUserAuthorisation, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty(InternalUserColumns)]
        });

        var user = await CreateRepository().GetByUserNameAsync(@"DEFRA\nobody", CancellationToken.None);

        user.Should().BeNull();
    }

    [Fact]
    public async Task GetByUserNameAsync_LogsAndRethrows_WhenTheProcedureFails()
    {
        connection.Script(UserStoredProcedures.GetUserAuthorisation, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = async () => await CreateRepository().GetByUserNameAsync(@"DEFRA\jdoe", CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task UpdateSsoUserIdIntAsync_ExecutesWithExpectedParameters()
    {
        connection.Script(UserStoredProcedures.UpdateSsoUserIdInt, new FakeCommandScript());

        await CreateRepository().UpdateSsoUserIdIntAsync(UserId, SsoUserIdInt, CancellationToken.None);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(UserStoredProcedures.UpdateSsoUserIdInt);
        executed.Parameters.Should().ContainKey("Id").WhoseValue.Should().Be(UserId);
        executed.Parameters.Should().ContainKey("SsoUserIdInt").WhoseValue.Should().Be(SsoUserIdInt);
    }

    [Fact]
    public async Task UpdateSsoUserIdIntAsync_LogsAndRethrows_WhenTheProcedureFails()
    {
        connection.Script(UserStoredProcedures.UpdateSsoUserIdInt, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = async () => await CreateRepository().UpdateSsoUserIdIntAsync(UserId, SsoUserIdInt, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetBySsoUserIdIntAsync_Throws_WhenConnectionFactoryDoesNotReturnADbConnection()
    {
        var repository = new UserRepository(new NonDbConnectionFactory(), logger.Object);

        var act = async () => await repository.GetBySsoUserIdIntAsync(SsoUserIdInt, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateInternalUserAsync_ExecutesWithExpectedParameters_AndReturnsTheStoredRow()
    {
        connection.Script(UserStoredProcedures.CreateInternalUser, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(InternalUserColumns,
                [
                    [UserId, @"DEFRA\jdoe", "Jane Internal", false, false, SsoUserIdInt]
                ])
            ]
        });

        var created = await CreateRepository().CreateInternalUserAsync(NewInternalUser(UserId), CancellationToken.None);

        created.Id.Should().Be(UserId);
        created.UserName.Should().Be(@"DEFRA\jdoe");
        created.FullName.Should().Be("Jane Internal");
        created.SsoUserIdInt.Should().Be(SsoUserIdInt);
        created.IsProfileEditor.Should().BeFalse();
        created.IsPolicyProfileUser.Should().BeFalse();

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(UserStoredProcedures.CreateInternalUser);
        executed.CommandType.Should().Be(CommandType.StoredProcedure);
        executed.Parameters.Should().ContainKey("Id").WhoseValue.Should().Be(UserId);
        executed.Parameters.Should().ContainKey("UserName").WhoseValue.Should().Be(@"DEFRA\jdoe");
        executed.Parameters.Should().ContainKey("FullName").WhoseValue.Should().Be("Jane Internal");
        executed.Parameters.Should().ContainKey("SsoUserIdInt").WhoseValue.Should().Be(SsoUserIdInt);
    }

    [Fact]
    public async Task CreateInternalUserAsync_ReturnsTheRowTheDatabaseHolds_WhenAnotherSignInCreatedItFirst()
    {
        var winnerId = Guid.Parse("55555555-5555-5555-5555-555555555555");

        connection.Script(UserStoredProcedures.CreateInternalUser, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(InternalUserColumns,
                [
                    [winnerId, @"DEFRA\jdoe", "Jane Internal", false, false, SsoUserIdInt]
                ])
            ]
        });

        var created = await CreateRepository().CreateInternalUserAsync(NewInternalUser(UserId), CancellationToken.None);

        created.Id.Should().Be(winnerId);
    }

    [Fact]
    public async Task CreateInternalUserAsync_LogsAndRethrows_WhenTheProcedureFails()
    {
        connection.Script(UserStoredProcedures.CreateInternalUser, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = async () => await CreateRepository().CreateInternalUserAsync(NewInternalUser(UserId), CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task CreateInternalUserAsync_Throws_WhenNewUserIsNull()
    {
        var act = async () => await CreateRepository().CreateInternalUserAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static InternalUser NewInternalUser(Guid id) => new()
    {
        Id = id,
        UserName = @"DEFRA\jdoe",
        FullName = "Jane Internal",
        SsoUserIdInt = SsoUserIdInt,
        IsProfileEditor = false,
        IsPolicyProfileUser = false
    };

    [Fact]
    public async Task GetActingUserAsync_MapsTheFlagsAndTheInternalIdentifier_OfAnInternalUser()
    {
        connection.Script(UserStoredProcedures.GetActingUser, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(ActingUserColumns, [[UserId, true, false, SsoUserIdInt, null]]),
                FakeResultSet.Empty(ProfileRoleColumns),
                FakeResultSet.Empty(SectionAssignmentColumns)
            ]
        });

        var user = await CreateRepository().GetActingUserAsync(UserId, CancellationToken.None);

        user.Should().NotBeNull();
        user.Id.Should().Be(UserId);
        user.IsProfileEditor.Should().BeTrue();
        user.IsPolicyProfileUser.Should().BeFalse();
        user.SsoUserIdInt.Should().Be(SsoUserIdInt);
        user.SsoUserIdExt.Should().BeNull();
        user.Kind.Should().Be(UserKind.Internal);
        user.ProfileRoles.Should().BeEmpty();
        user.SectionAssignments.Should().BeEmpty();

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(UserStoredProcedures.GetActingUser);
        executed.CommandType.Should().Be(CommandType.StoredProcedure);
        executed.Parameters.Should().ContainKey("UserId").WhoseValue.Should().Be(UserId);
    }

    [Fact]
    public async Task GetActingUserAsync_MapsTheProfileRolesAndSectionAssignments_OfAnExternalUser()
    {
        var profileId = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000000a");
        var otherProfileId = Guid.Parse("aaaaaaaa-0000-0000-0000-00000000000b");
        var roleId = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000000a");
        var otherRoleId = Guid.Parse("bbbbbbbb-0000-0000-0000-00000000000b");
        var sectionId = Guid.Parse("cccccccc-0000-0000-0000-00000000000a");
        connection.Script(UserStoredProcedures.GetActingUser, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(ActingUserColumns, [[UserId, false, false, null, SsoUserIdExt]]),
                new FakeResultSet(
                    ProfileRoleColumns,
                    [
                        [profileId, roleId, "Technical author", true],
                        [otherProfileId, otherRoleId, "Policy reviewer", false]
                    ]),
                new FakeResultSet(SectionAssignmentColumns, [[profileId, sectionId]])
            ]
        });

        var user = await CreateRepository().GetActingUserAsync(UserId, CancellationToken.None);

        user.Should().NotBeNull();
        user.Kind.Should().Be(UserKind.External);
        user.SsoUserIdExt.Should().Be(SsoUserIdExt);
        user.ProfileRoles.Should().Equal(
            new ProfileRoleAssignment(profileId, roleId, "Technical author", true),
            new ProfileRoleAssignment(otherProfileId, otherRoleId, "Policy reviewer", false));
        user.SectionAssignments.Should().Equal(new ProfileSectionAssignment(profileId, sectionId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task GetActingUserAsync_LeavesTheUserUnclassified_WhenNeitherOrBothIdentifiersAreSet(bool bothSet)
    {
        connection.Script(UserStoredProcedures.GetActingUser, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(ActingUserColumns, [[UserId, true, true, bothSet ? SsoUserIdInt : null, bothSet ? SsoUserIdExt : null]])
            ]
        });

        var user = await CreateRepository().GetActingUserAsync(UserId, CancellationToken.None);

        user.Should().NotBeNull();
        user.Kind.Should().Be(UserKind.Unclassified);
        user.ProfileRoles.Should().BeEmpty();
        user.SectionAssignments.Should().BeEmpty();
    }

    [Fact]
    public async Task GetActingUserAsync_ReturnsNull_WhenNotFound()
    {
        connection.Script(UserStoredProcedures.GetActingUser, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty(ActingUserColumns)]
        });

        var user = await CreateRepository().GetActingUserAsync(UserId, CancellationToken.None);

        user.Should().BeNull();
    }

    [Fact]
    public async Task GetActingUserAsync_LogsAndRethrows_WhenTheProcedureFails()
    {
        connection.Script(UserStoredProcedures.GetActingUser, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = async () => await CreateRepository().GetActingUserAsync(UserId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
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
