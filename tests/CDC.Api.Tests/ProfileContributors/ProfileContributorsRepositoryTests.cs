using System.Data;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileContributors.Commands;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.ProfileContributors;

public class ProfileContributorsRepositoryTests : IDisposable
{
    private static readonly Guid ProfileId = Guid.NewGuid();

    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<ProfileContributorsRepository>> logger = new();

    public ProfileContributorsRepositoryTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private ProfileContributorsRepository CreateRepository() =>
        new(new StubConnectionFactory(connection), logger.Object);

    [Fact]
    public async Task GetProfileContributorsAsync_ExecutesStoredProcedureAndMapsRows()
    {
        var contributorId = Guid.NewGuid();
        connection.Script(ProfileContributorsStoredProcedures.GetProfileContributorsByProfileId, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "UserName", "FullName", "Organisation", "Name"],
                    [[contributorId, "carrie.batten", "Carrie Batten", "Pirbright Institute", "Technical author"]])
            ]
        });

        var contributors = await CreateRepository().GetProfileContributorsAsync(ProfileId, CancellationToken.None);

        contributors.Should().ContainSingle();
        contributors[0].Id.Should().Be(contributorId);
        contributors[0].UserName.Should().Be("carrie.batten");
        contributors[0].FullName.Should().Be("Carrie Batten");
        contributors[0].Organisation.Should().Be("Pirbright Institute");
        contributors[0].Role.Should().Be("Technical author");

        var recorded = connection.Executed.Should().ContainSingle().Subject;
        recorded.CommandText.Should().Be(ProfileContributorsStoredProcedures.GetProfileContributorsByProfileId);
        recorded.CommandType.Should().Be(CommandType.StoredProcedure);
        recorded.Parameters.Should().ContainKey("ProfileId").WhoseValue.Should().Be(ProfileId);
    }

    [Fact]
    public async Task GetProfileContributorsAsync_ReturnsEmpty_WhenTheProfileHasNoContributors()
    {
        connection.Script(ProfileContributorsStoredProcedures.GetProfileContributorsByProfileId, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty("Id", "UserName", "FullName", "Organisation", "Name")]
        });

        var contributors = await CreateRepository().GetProfileContributorsAsync(ProfileId, CancellationToken.None);

        contributors.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProfileContributorsAsync_LogsAndRethrows_WhenTheStoredProcedureFails()
    {
        connection.Script(ProfileContributorsStoredProcedures.GetProfileContributorsByProfileId, new FakeCommandScript
        {
            Throws = new FakeDbException("boom")
        });

        var act = () => CreateRepository().GetProfileContributorsAsync(ProfileId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    private static readonly string[] ContributorColumns =
        ["Id", "UserName", "FullName", "Organisation", "ProfileUserRoleId", "Name", "IsContributor", "SsoUserId", "LastUpdated"];

    [Fact]
    public async Task GetContributorForEditAsync_ReadsBothResultSets_WhenTheContributorIsAProfileUser()
    {
        var contributorId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var ssoUserId = Guid.NewGuid();
        var sectionId1 = Guid.NewGuid();
        var sectionId2 = Guid.NewGuid();
        var rowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        connection.Script(ProfileContributorsStoredProcedures.GetContributor, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ContributorColumns,
                    [[contributorId, "carrie.batten", "Carrie Batten", "Pirbright Institute", roleId, "Technical author", true, ssoUserId, rowVersion]]),
                new FakeResultSet(["ProfileSectionId"], [[sectionId1], [sectionId2]])
            ]
        });

        var contributor = await CreateRepository().GetContributorForEditAsync(ProfileId, contributorId, CancellationToken.None);

        contributor.Should().NotBeNull();
        contributor!.Id.Should().Be(contributorId);
        contributor.UserName.Should().Be("carrie.batten");
        contributor.RoleId.Should().Be(roleId);
        contributor.IsSsoUser.Should().BeTrue();
        contributor.SectionPermissionIds.Should().BeEquivalentTo([sectionId1, sectionId2]);
        contributor.LastUpdated.Should().Equal(rowVersion);
    }

    [Fact]
    public async Task GetContributorForEditAsync_ReturnsNull_WhenTheContributorHasNoRows()
    {
        connection.Script(ProfileContributorsStoredProcedures.GetContributor, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty(ContributorColumns)]
        });

        var contributor = await CreateRepository().GetContributorForEditAsync(ProfileId, Guid.NewGuid(), CancellationToken.None);

        contributor.Should().BeNull();
    }

    [Fact]
    public async Task GetContributorForEditAsync_ReturnsNoPermissions_WhenTheUserIsNotAProfileUser()
    {
        var contributorId = Guid.NewGuid();
        var rowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

        connection.Script(ProfileContributorsStoredProcedures.GetContributor, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ContributorColumns,
                    [[contributorId, "carrie.batten", "Carrie Batten", "Pirbright Institute", null, null, null, null, rowVersion]])
            ]
        });

        var contributor = await CreateRepository().GetContributorForEditAsync(ProfileId, contributorId, CancellationToken.None);

        contributor.Should().NotBeNull();
        contributor!.RoleId.Should().Be(Guid.Empty);
        contributor.IsSsoUser.Should().BeFalse();
        contributor.SectionPermissionIds.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProfileUserRolesAsync_ExecutesStoredProcedureAndMapsRows()
    {
        var roleId = Guid.NewGuid();
        connection.Script(ProfileContributorsStoredProcedures.GetProfileUserRoles, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["Id", "Name", "IsContributor"], [[roleId, "Technical author", true]])]
        });

        var roles = await CreateRepository().GetProfileUserRolesAsync(CancellationToken.None);

        roles.Should().ContainSingle();
        roles[0].Id.Should().Be(roleId);
        roles[0].Name.Should().Be("Technical author");
        roles[0].IsContributor.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateContributorAsync_UpsertsAndDiffsSectionPermissions_WhenTheUpdateSucceeds()
    {
        var contributorId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var keptSectionId = Guid.NewGuid();
        var removedSectionId = Guid.NewGuid();
        var addedSectionId = Guid.NewGuid();
        var newRowVersion = new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 };

        connection.Script(ProfileContributorsStoredProcedures.GetContributor, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ContributorColumns,
                    [[contributorId, "carrie.batten", "Carrie Batten", "Pirbright Institute", roleId, "Technical author", true, null, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }]]),
                new FakeResultSet(["ProfileSectionId"], [[keptSectionId], [removedSectionId]])
            ]
        });
        connection.Script(ProfileContributorsStoredProcedures.UpsertContributor, new FakeCommandScript
        {
            OutputValues = new Dictionary<string, object?> { ["NewLastUpdated"] = newRowVersion }
        });
        connection.Script(ProfileContributorsStoredProcedures.AddSectionPermission, new FakeCommandScript());
        connection.Script(ProfileContributorsStoredProcedures.RemoveSectionPermission, new FakeCommandScript());

        var command = new UpdateContributorCommand(
            ProfileId,
            contributorId,
            roleId,
            "Carrie Batten",
            "Pirbright Institute",
            [keptSectionId, addedSectionId],
            [1, 2, 3, 4, 5, 6, 7, 8]);

        var result = await CreateRepository().UpdateContributorAsync(command, CancellationToken.None);

        result.Should().Equal(newRowVersion);

        var addCall = connection.Executed.Should().ContainSingle(
            call => call.CommandText == ProfileContributorsStoredProcedures.AddSectionPermission).Subject;
        addCall.Parameters["ProfileSectionId"].Should().Be(addedSectionId);

        var removeCall = connection.Executed.Should().ContainSingle(
            call => call.CommandText == ProfileContributorsStoredProcedures.RemoveSectionPermission).Subject;
        removeCall.Parameters["ProfileSectionId"].Should().Be(removedSectionId);
    }

    [Fact]
    public async Task UpdateContributorAsync_ReturnsNull_WhenTheContributorDoesNotExist()
    {
        connection.Script(ProfileContributorsStoredProcedures.GetContributor, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty(ContributorColumns)]
        });

        var command = new UpdateContributorCommand(
            ProfileId, Guid.NewGuid(), Guid.NewGuid(), "Carrie Batten", "Pirbright Institute", [], [1, 2, 3, 4, 5, 6, 7, 8]);

        var result = await CreateRepository().UpdateContributorAsync(command, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateContributorAsync_ThrowsConcurrencyException_WhenTheRowVersionHasMovedOn()
    {
        var contributorId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        connection.Script(ProfileContributorsStoredProcedures.GetContributor, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ContributorColumns,
                    [[contributorId, "carrie.batten", "Carrie Batten", "Pirbright Institute", roleId, "Technical author", true, null, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }]]),
                new FakeResultSet(["ProfileSectionId"], [])
            ]
        });
        connection.Script(ProfileContributorsStoredProcedures.UpsertContributor, new FakeCommandScript
        {
            Throws = new FakeDbException("The user information has been edited by another user")
        });

        var command = new UpdateContributorCommand(
            ProfileId, contributorId, roleId, "Carrie Batten", "Pirbright Institute", [], [1, 2, 3, 4, 5, 6, 7, 8]);

        var act = () => CreateRepository().UpdateContributorAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    [Fact]
    public async Task FindUserByUsernameAsync_ReturnsTheMappedUser_WhenFound()
    {
        var userId = Guid.NewGuid();
        connection.Script(ProfileContributorsStoredProcedures.FindUserByUsername, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ["Id", "FullName", "Organisation", "IsProfileEditor", "IsPolicyProfileUser", "UserName", "SsoUserId", "IsUserManagementSystem"],
                    [[userId, "Carrie Batten", "Pirbright Institute", false, false, "internal\\carrie.batten", null, true]])
            ]
        });

        var user = await CreateRepository().FindUserByUsernameAsync("internal\\carrie.batten", CancellationToken.None);

        user.Should().NotBeNull();
        user!.Id.Should().Be(userId);
        user.IsUserManagementSystem.Should().BeTrue();
    }

    [Fact]
    public async Task FindUserByUsernameAsync_ReturnsNull_WhenNotFound()
    {
        connection.Script(ProfileContributorsStoredProcedures.FindUserByUsername, new FakeCommandScript
        {
            ResultSets =
            [
                FakeResultSet.Empty(
                    "Id", "FullName", "Organisation", "IsProfileEditor", "IsPolicyProfileUser", "UserName", "SsoUserId", "IsUserManagementSystem")
            ]
        });

        var user = await CreateRepository().FindUserByUsernameAsync("internal\\unknown", CancellationToken.None);

        user.Should().BeNull();
    }

    [Fact]
    public async Task AddContributorAsync_InsertsANewGlobalUser_WhenTheContributorDoesNotExistYet()
    {
        var contributorId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();

        connection.Script(ProfileContributorsStoredProcedures.GetContributor, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty(ContributorColumns)]
        });
        connection.Script(ProfileContributorsStoredProcedures.UpsertContributor, new FakeCommandScript
        {
            OutputValues = new Dictionary<string, object?> { ["NewLastUpdated"] = new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 } }
        });
        connection.Script(ProfileContributorsStoredProcedures.AddSectionPermission, new FakeCommandScript());

        var command = new AddContributorCommand(
            ProfileId, contributorId, "internal\\new.user", false, roleId, "New User", "Pirbright Institute", [sectionId]);

        await CreateRepository().AddContributorAsync(command, CancellationToken.None);

        var upsertCall = connection.Executed.Should().ContainSingle(
            call => call.CommandText == ProfileContributorsStoredProcedures.UpsertContributor).Subject;
        upsertCall.Parameters["UserName"].Should().Be("internal\\new.user");
        upsertCall.Parameters["FullName"].Should().Be("New User");

        var addCall = connection.Executed.Should().ContainSingle(
            call => call.CommandText == ProfileContributorsStoredProcedures.AddSectionPermission).Subject;
        addCall.Parameters["ProfileSectionId"].Should().Be(sectionId);
    }

    [Fact]
    public async Task AddContributorAsync_UpsertsAndGrantsSectionPermissions_WhenTheGlobalUserAlreadyExists()
    {
        var contributorId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var addedSectionId = Guid.NewGuid();

        connection.Script(ProfileContributorsStoredProcedures.GetContributor, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ContributorColumns,
                    [[contributorId, "carrie.batten", "Carrie Batten", "Pirbright Institute", null, null, null, null, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }]])
            ]
        });
        connection.Script(ProfileContributorsStoredProcedures.UpsertContributor, new FakeCommandScript
        {
            OutputValues = new Dictionary<string, object?> { ["NewLastUpdated"] = new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 } }
        });
        connection.Script(ProfileContributorsStoredProcedures.AddSectionPermission, new FakeCommandScript());

        var command = new AddContributorCommand(
            ProfileId, contributorId, "ignored-not-yet-registered", false, roleId, "Carrie Batten", "Pirbright Institute", [addedSectionId]);

        await CreateRepository().AddContributorAsync(command, CancellationToken.None);

        var upsertCall = connection.Executed.Should().ContainSingle(
            call => call.CommandText == ProfileContributorsStoredProcedures.UpsertContributor).Subject;
        upsertCall.Parameters["UserName"].Should().Be("carrie.batten");

        var addCall = connection.Executed.Should().ContainSingle(
            call => call.CommandText == ProfileContributorsStoredProcedures.AddSectionPermission).Subject;
        addCall.Parameters["ProfileSectionId"].Should().Be(addedSectionId);
        connection.Executed.Should().NotContain(call => call.CommandText == ProfileContributorsStoredProcedures.RemoveSectionPermission);
    }

    [Fact]
    public async Task AddContributorAsync_UsesTheExistingFullNameAndOrganisation_ForAnSsoUser()
    {
        var contributorId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        connection.Script(ProfileContributorsStoredProcedures.GetContributor, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ContributorColumns,
                    [[contributorId, "carrie.batten", "Carrie Batten", "Pirbright Institute", null, null, null, Guid.NewGuid(), new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }]])
            ]
        });
        connection.Script(ProfileContributorsStoredProcedures.UpsertContributor, new FakeCommandScript
        {
            OutputValues = new Dictionary<string, object?> { ["NewLastUpdated"] = new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 } }
        });

        var command = new AddContributorCommand(
            ProfileId, contributorId, "ignored", true, roleId, "Tampered Name", "Tampered Org", []);

        await CreateRepository().AddContributorAsync(command, CancellationToken.None);

        var upsertCall = connection.Executed.Should().ContainSingle(
            call => call.CommandText == ProfileContributorsStoredProcedures.UpsertContributor).Subject;
        upsertCall.Parameters["FullName"].Should().Be("Carrie Batten");
        upsertCall.Parameters["Organisation"].Should().Be("Pirbright Institute");
    }

    [Fact]
    public async Task AddContributorAsync_ThrowsDuplicateUsernameException_WhenTheUsernameIsAlreadyInUse()
    {
        connection.Script(ProfileContributorsStoredProcedures.GetContributor, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty(ContributorColumns)]
        });
        connection.Script(ProfileContributorsStoredProcedures.UpsertContributor, new FakeCommandScript
        {
            Throws = new FakeDbException("There is already a user with the specified username")
        });

        var command = new AddContributorCommand(
            ProfileId, Guid.NewGuid(), "internal\\duplicate", false, Guid.NewGuid(), "Name", "Org", []);

        var act = () => CreateRepository().AddContributorAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateUsernameException>();
    }

    [Fact]
    public async Task AddContributorAsync_ThrowsConcurrencyException_WhenTheExistingUsersRowVersionHasMovedOn()
    {
        var contributorId = Guid.NewGuid();

        connection.Script(ProfileContributorsStoredProcedures.GetContributor, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    ContributorColumns,
                    [[contributorId, "carrie.batten", "Carrie Batten", "Pirbright Institute", null, null, null, null, new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }]])
            ]
        });
        connection.Script(ProfileContributorsStoredProcedures.UpsertContributor, new FakeCommandScript
        {
            Throws = new FakeDbException("The user information has been edited by another user")
        });

        var command = new AddContributorCommand(
            ProfileId, contributorId, "ignored", false, Guid.NewGuid(), "Name", "Org", []);

        var act = () => CreateRepository().AddContributorAsync(command, CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    [Fact]
    public async Task DeleteContributorAsync_ExecutesTheStoredProcedureWithTheRightParameters()
    {
        var contributorId = Guid.NewGuid();
        var lastUpdated = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        connection.Script(ProfileContributorsStoredProcedures.DeleteContributor, new FakeCommandScript());

        await CreateRepository().DeleteContributorAsync(ProfileId, contributorId, lastUpdated, CancellationToken.None);

        var recorded = connection.Executed.Should().ContainSingle().Subject;
        recorded.CommandText.Should().Be(ProfileContributorsStoredProcedures.DeleteContributor);
        recorded.Parameters["UserId"].Should().Be(contributorId);
        recorded.Parameters["ProfileId"].Should().Be(ProfileId);
    }

    [Fact]
    public async Task DeleteContributorAsync_ThrowsConcurrencyException_WhenTheRowVersionHasMovedOn()
    {
        connection.Script(ProfileContributorsStoredProcedures.DeleteContributor, new FakeCommandScript
        {
            Throws = new FakeDbException("The user has been edited by another user")
        });

        var act = () => CreateRepository().DeleteContributorAsync(ProfileId, Guid.NewGuid(), [1, 2, 3, 4, 5, 6, 7, 8], CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }
}
