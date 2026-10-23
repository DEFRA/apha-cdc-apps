using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.GlobalUsers;
using CDC.Api.Features.GlobalUsers.Commands;
using CDC.Api.Features.GlobalUsers.Interfaces;
using CDC.Common.Contracts;
using CDC.Graph;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.GlobalUsers;

public class GlobalUsersServiceTests
{
    private readonly Mock<IGlobalUsersRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<IGraphClient> graph = new();

    private GlobalUsersService CreateService() => new(repository.Object, graph.Object, NullLogger<GlobalUsersService>.Instance);

    private static GlobalUser User(string userName, GlobalUserRole role = GlobalUserRole.ProfileEditor, bool isSso = false) => new()
    {
        Id = Guid.NewGuid(),
        UserName = userName,
        FullName = $"{userName} full name",
        Organisation = "APHA",
        Role = role,
        SubscribedToReviewEmails = true,
        IsSsoUser = isSso,
        LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
    };

    private static SaveGlobalUserCommand Save(
        Guid? id = null,
        bool isNew = false,
        string userName = @"DEFRA\jsmith",
        string fullName = "Jo Smith",
        string organisation = "APHA",
        GlobalUserRole role = GlobalUserRole.PolicyProfileUser) =>
        new(id ?? Guid.NewGuid(), isNew, userName, fullName, organisation, role, false, [1, 2, 3, 4, 5, 6, 7, 8]);

    [Fact]
    public async Task GetGlobalUsersAsync_ReturnsOnlyThatPage()
    {
        repository
            .Setup(repo => repo.GetGlobalUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([User(@"D\a"), User(@"D\b"), User(@"D\c")]);

        var result = await CreateService().GetGlobalUsersAsync(pageNumber: 2, pageSize: 2, GlobalUserSortColumn.UserName, sortDescending: false, CancellationToken.None);

        result.Items.Should().ContainSingle().Which.UserName.Should().Be(@"D\c");
        result.TotalRecords.Should().Be(3);
        result.TotalPages.Should().Be(2);
    }

    [Fact]
    public async Task GetGlobalUsersAsync_SortsByFullNameBeforePaging_ByDefault()
    {
        repository
            .Setup(repo => repo.GetGlobalUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([User(@"D\c"), User(@"D\a"), User(@"D\b")]);

        var result = await CreateService().GetGlobalUsersAsync(pageNumber: 1, pageSize: 2, GlobalUserSortColumn.FullName, sortDescending: false, CancellationToken.None);

        result.Items.Select(user => user.UserName).Should().Equal(@"D\a", @"D\b");
    }

    [Fact]
    public async Task GetGlobalUsersAsync_SortsDescending()
    {
        repository
            .Setup(repo => repo.GetGlobalUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([User(@"D\a"), User(@"D\c"), User(@"D\b")]);

        var result = await CreateService().GetGlobalUsersAsync(pageNumber: 1, pageSize: 0, GlobalUserSortColumn.UserName, sortDescending: true, CancellationToken.None);

        result.Items.Select(user => user.UserName).Should().Equal(@"D\c", @"D\b", @"D\a");
    }

    [Fact]
    public async Task GetGlobalUsersAsync_SortsRoleByName()
    {
        repository
            .Setup(repo => repo.GetGlobalUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([User(@"D\editor", GlobalUserRole.ProfileEditor), User(@"D\policy", GlobalUserRole.PolicyProfileUser)]);

        var result = await CreateService().GetGlobalUsersAsync(pageNumber: 1, pageSize: 0, GlobalUserSortColumn.Role, sortDescending: false, CancellationToken.None);

        result.Items.Select(user => user.Role).Should().Equal(GlobalUserRole.PolicyProfileUser, GlobalUserRole.ProfileEditor);
    }

    [Fact]
    public async Task GetGlobalUsersAsync_SortsByOrganisation()
    {
        var apha = User(@"D\a");
        var defra = User(@"D\b") with { Organisation = "Defra" };
        repository
            .Setup(repo => repo.GetGlobalUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([defra, apha]);

        var result = await CreateService().GetGlobalUsersAsync(pageNumber: 1, pageSize: 0, GlobalUserSortColumn.Organisation, sortDescending: false, CancellationToken.None);

        result.Items.Select(user => user.Organisation).Should().Equal("APHA", "Defra");
    }

    [Fact]
    public async Task GetGlobalUsersAsync_ReturnsEveryUser_WhenPageSizeIsZero()
    {
        repository
            .Setup(repo => repo.GetGlobalUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([User(@"D\a"), User(@"D\b"), User(@"D\c")]);

        var result = await CreateService().GetGlobalUsersAsync(pageNumber: 1, pageSize: 0, GlobalUserSortColumn.FullName, sortDescending: false, CancellationToken.None);

        result.Items.Should().HaveCount(3);
        result.TotalPages.Should().Be(1);
    }

    [Fact]
    public async Task LookupAsync_ReturnsExistingUserDetails()
    {
        var user = User(@"DEFRA\jsmith", GlobalUserRole.PolicyProfileUser);
        repository.Setup(repo => repo.FindUserByUsernameAsync(user.UserName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserVerification { Id = user.Id, IsUserManagementSystem = false });
        repository.Setup(repo => repo.GetUserAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await CreateService().LookupAsync(user.UserName, CancellationToken.None);

        result.Outcome.Should().Be(GlobalUserLookupOutcome.ExistingUser);
        result.Id.Should().Be(user.Id);
        result.FullName.Should().Be(user.FullName);
        result.Role.Should().Be(GlobalUserRole.PolicyProfileUser);
        result.LastUpdated.Should().Equal(user.LastUpdated);
    }

    [Fact]
    public async Task LookupAsync_Blocks_UserManagementSystemAccounts()
    {
        repository.Setup(repo => repo.FindUserByUsernameAsync(@"DEFRA\system", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserVerification { Id = Guid.NewGuid(), IsUserManagementSystem = true });

        var result = await CreateService().LookupAsync(@"DEFRA\system", CancellationToken.None);

        result.Outcome.Should().Be(GlobalUserLookupOutcome.Blocked);
    }

    [Fact]
    public async Task LookupAsync_RejectsSsoUsers()
    {
        var user = User(@"DEFRA\sso", isSso: true);
        repository.Setup(repo => repo.FindUserByUsernameAsync(user.UserName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserVerification { Id = user.Id, IsUserManagementSystem = false });
        repository.Setup(repo => repo.GetUserAsync(user.Id, It.IsAny<CancellationToken>())).ReturnsAsync(user);

        var result = await CreateService().LookupAsync(user.UserName, CancellationToken.None);

        result.Outcome.Should().Be(GlobalUserLookupOutcome.SsoUser);
    }

    [Fact]
    public async Task LookupAsync_ReturnsNewUser_WhenUnknownButWellFormed()
    {
        repository.Setup(repo => repo.FindUserByUsernameAsync(@"DEFRA\new.user", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserVerification?)null);

        var result = await CreateService().LookupAsync(@"DEFRA\new.user", CancellationToken.None);

        result.Outcome.Should().Be(GlobalUserLookupOutcome.NewUser);
        result.Id.Should().NotBeEmpty();
        result.UserName.Should().Be(@"DEFRA\new.user");
        result.FullName.Should().BeEmpty();
        result.Organisation.Should().BeEmpty();
    }

    [Fact]
    public async Task LookupAsync_PrefillsANewUser_FromTheDirectory()
    {
        repository.Setup(repo => repo.FindUserByUsernameAsync(@"DEFRA\new.user", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserVerification?)null);
        graph.Setup(client => client.FindUserBySamAccountNameAsync("new.user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphUser("id-1", "  New User  ", "APHA"));

        var result = await CreateService().LookupAsync(@"DEFRA\new.user", CancellationToken.None);

        result.Outcome.Should().Be(GlobalUserLookupOutcome.NewUser);
        result.FullName.Should().Be("New User");
        result.Organisation.Should().Be("APHA");
    }

    [Fact]
    public async Task LookupAsync_TruncatesDirectoryValuesToTheSavedLength()
    {
        repository.Setup(repo => repo.FindUserByUsernameAsync(@"DEFRA\new.user", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserVerification?)null);
        graph.Setup(client => client.FindUserBySamAccountNameAsync("new.user", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GraphUser("id-1", new string('n', 150), null));

        var result = await CreateService().LookupAsync(@"DEFRA\new.user", CancellationToken.None);

        result.FullName.Should().HaveLength(100);
        result.Organisation.Should().BeEmpty();
    }

    [Fact]
    public async Task LookupAsync_StillReturnsANewUser_WhenTheDirectoryLookupFails()
    {
        repository.Setup(repo => repo.FindUserByUsernameAsync(@"DEFRA\new.user", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserVerification?)null);
        graph.Setup(client => client.FindUserBySamAccountNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new GraphException("down"));

        var result = await CreateService().LookupAsync(@"DEFRA\new.user", CancellationToken.None);

        result.Outcome.Should().Be(GlobalUserLookupOutcome.NewUser);
        result.FullName.Should().BeEmpty();
    }

    [Fact]
    public async Task LookupAsync_DoesNotAskTheDirectory_ForAnExistingOrMalformedUser()
    {
        repository.Setup(repo => repo.FindUserByUsernameAsync("nodomain", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserVerification?)null);

        await CreateService().LookupAsync("nodomain", CancellationToken.None);

        graph.Verify(client => client.FindUserBySamAccountNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task LookupAsync_ReturnsInvalidFormat_WhenUnknownAndMalformed()
    {
        repository.Setup(repo => repo.FindUserByUsernameAsync("nodomain", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserVerification?)null);

        var result = await CreateService().LookupAsync("nodomain", CancellationToken.None);

        result.Outcome.Should().Be(GlobalUserLookupOutcome.InvalidFormat);
    }

    [Fact]
    public async Task SaveAsync_SavesNewUser()
    {
        var command = Save(isNew: true);
        repository.Setup(repo => repo.GetUserAsync(command.Id, It.IsAny<CancellationToken>())).ReturnsAsync((GlobalUser?)null);
        GlobalUser? saved = null;
        repository.Setup(repo => repo.SaveGlobalUserAsync(It.IsAny<GlobalUser>(), It.IsAny<CancellationToken>()))
            .Callback<GlobalUser, CancellationToken>((user, _) => saved = user)
            .Returns(Task.CompletedTask);

        var result = await CreateService().SaveAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        saved!.UserName.Should().Be(@"DEFRA\jsmith");
        saved.Role.Should().Be(GlobalUserRole.PolicyProfileUser);
    }

    [Fact]
    public async Task SaveAsync_KeepsStoredUserName_WhenEditing()
    {
        var existing = User(@"DEFRA\stored");
        var command = Save(existing.Id, userName: @"DEFRA\tampered");
        repository.Setup(repo => repo.GetUserAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        GlobalUser? saved = null;
        repository.Setup(repo => repo.SaveGlobalUserAsync(It.IsAny<GlobalUser>(), It.IsAny<CancellationToken>()))
            .Callback<GlobalUser, CancellationToken>((user, _) => saved = user)
            .Returns(Task.CompletedTask);

        var result = await CreateService().SaveAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        saved!.UserName.Should().Be(@"DEFRA\stored");
    }

    [Fact]
    public async Task SaveAsync_AllowsEditing_WhenStoredUserNameIsNotDomainFormat()
    {
        var existing = User("jo.smith@defra.gov.uk");
        repository.Setup(repo => repo.GetUserAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);
        repository.Setup(repo => repo.SaveGlobalUserAsync(It.IsAny<GlobalUser>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateService().SaveAsync(Save(existing.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SaveAsync_ReturnsNotFound_WhenEditingUnknownUser()
    {
        var command = Save();
        repository.Setup(repo => repo.GetUserAsync(command.Id, It.IsAny<CancellationToken>())).ReturnsAsync((GlobalUser?)null);

        var result = await CreateService().SaveAsync(command, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task SaveAsync_Rejects_SsoUser()
    {
        var existing = User(@"DEFRA\sso", isSso: true);
        repository.Setup(repo => repo.GetUserAsync(existing.Id, It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var result = await CreateService().SaveAsync(Save(existing.Id), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.ValidationFailed);
    }

    [Theory]
    [InlineData(GlobalUserRole.None, "Jo Smith", "APHA")]
    [InlineData(GlobalUserRole.ProfileEditor, " ", "APHA")]
    [InlineData(GlobalUserRole.ProfileEditor, "Jo Smith", "")]
    public async Task SaveAsync_ReturnsValidationFailed_ForMissingFields(GlobalUserRole role, string fullName, string organisation)
    {
        var command = Save(isNew: true, role: role, fullName: fullName, organisation: organisation);
        repository.Setup(repo => repo.GetUserAsync(command.Id, It.IsAny<CancellationToken>())).ReturnsAsync((GlobalUser?)null);

        var result = await CreateService().SaveAsync(command, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.ValidationFailed);
    }

    [Fact]
    public async Task SaveAsync_ReturnsValidationFailed_ForOverlongFullName()
    {
        var command = Save(isNew: true, fullName: new string('a', 101));
        repository.Setup(repo => repo.GetUserAsync(command.Id, It.IsAny<CancellationToken>())).ReturnsAsync((GlobalUser?)null);

        var result = await CreateService().SaveAsync(command, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.ValidationFailed);
    }

    [Fact]
    public async Task SaveAsync_ReturnsValidationFailed_ForOverlongOrganisation()
    {
        var command = Save(isNew: true, organisation: new string('a', 101));
        repository.Setup(repo => repo.GetUserAsync(command.Id, It.IsAny<CancellationToken>())).ReturnsAsync((GlobalUser?)null);

        var result = await CreateService().SaveAsync(command, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.ValidationFailed);
    }

    [Fact]
    public async Task SaveAsync_ReturnsValidationFailed_ForMalformedNewUserName()
    {
        var command = Save(isNew: true, userName: "nodomain");
        repository.Setup(repo => repo.GetUserAsync(command.Id, It.IsAny<CancellationToken>())).ReturnsAsync((GlobalUser?)null);

        var result = await CreateService().SaveAsync(command, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.ValidationFailed);
    }

    [Fact]
    public async Task SaveAsync_ReturnsConflict_WhenRowVersionMoved()
    {
        var command = Save(isNew: true);
        repository.Setup(repo => repo.GetUserAsync(command.Id, It.IsAny<CancellationToken>())).ReturnsAsync((GlobalUser?)null);
        repository.Setup(repo => repo.SaveGlobalUserAsync(It.IsAny<GlobalUser>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("edited by another user", new InvalidOperationException()));

        var result = await CreateService().SaveAsync(command, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public async Task SaveAsync_ReturnsValidationFailed_WhenUsernameInUse()
    {
        var command = Save(isNew: true);
        repository.Setup(repo => repo.GetUserAsync(command.Id, It.IsAny<CancellationToken>())).ReturnsAsync((GlobalUser?)null);
        repository.Setup(repo => repo.SaveGlobalUserAsync(It.IsAny<GlobalUser>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateUsernameException("in use", new InvalidOperationException()));

        var result = await CreateService().SaveAsync(command, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.ValidationFailed);
    }

    [Fact]
    public async Task DeleteAsync_Succeeds()
    {
        var id = Guid.NewGuid();
        byte[] version = [1, 2, 3, 4, 5, 6, 7, 8];
        repository.Setup(repo => repo.DeleteGlobalUserAsync(id, version, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await CreateService().DeleteAsync(new DeleteGlobalUserCommand(id, version), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.VerifyAll();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsConflict_WhenRowVersionMoved()
    {
        repository.Setup(repo => repo.DeleteGlobalUserAsync(It.IsAny<Guid>(), It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("edited by another user", new InvalidOperationException()));

        var result = await CreateService().DeleteAsync(new DeleteGlobalUserCommand(Guid.NewGuid(), [1]), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
    }
}
