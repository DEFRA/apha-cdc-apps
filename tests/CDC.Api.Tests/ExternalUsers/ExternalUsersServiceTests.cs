using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ExternalUsers;
using CDC.Api.Features.ExternalUsers.Commands;
using CDC.Api.Features.ExternalUsers.Interfaces;
using CDC.Api.Features.ProfileContributors.Commands;
using CDC.Api.Features.ProfileContributors.Dtos;
using CDC.Api.Features.ProfileContributors.Interfaces;
using CDC.Common.Contracts;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ExternalUsers;

public class ExternalUsersServiceTests
{
    private readonly Mock<IExternalUsersRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<IProfileContributorsService> contributors = new(MockBehavior.Strict);

    private ExternalUsersService CreateService() => new(repository.Object, contributors.Object, NullLogger<ExternalUsersService>.Instance);

    private static ExternalUserAccount User(string userName, bool subscribed) => new()
    {
        Id = Guid.NewGuid(),
        UserName = userName,
        FullName = $"{userName} full name",
        EmailAddress = $"{userName}@example.test",
        Organisation = "Org",
        SubscribedToReviewEmails = subscribed,
        SsoUserId = Guid.NewGuid()
    };

    private void SetupAwaitingInstructions(params Guid[] userIds) =>
        repository
            .Setup(repo => repo.GetUsersAwaitingLoginInstructionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(userIds.ToHashSet());

    [Fact]
    public async Task GetExternalUsersAsync_ReturnsOnlyThatPage_WithSubscriptionState()
    {
        SetupAwaitingInstructions();
        repository
            .Setup(repo => repo.GetExternalUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([User("a", true), User("b", false), User("c", true)]);

        var result = await CreateService().GetExternalUsersAsync(
            pageNumber: 2, pageSize: 2, ExternalUserSortColumn.None, sortDescending: false, CancellationToken.None);

        result.Items.Should().ContainSingle().Which.SubscribedToReviewEmails.Should().BeTrue();
        result.TotalRecords.Should().Be(3);
    }

    [Fact]
    public async Task GetExternalUsersAsync_MarksOnlyTheUsersStillAwaitingInstructionsAsNotSent()
    {
        var awaiting = User("new", false);
        var sent = User("old", false);
        SetupAwaitingInstructions(awaiting.Id);
        repository
            .Setup(repo => repo.GetExternalUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([awaiting, sent]);

        var result = await CreateService().GetExternalUsersAsync(1, 0, ExternalUserSortColumn.None, sortDescending: false, CancellationToken.None);

        result.Items.Single(user => user.UserName == "new").LoginInstructionsSent.Should().BeFalse();
        result.Items.Single(user => user.UserName == "old").LoginInstructionsSent.Should().BeTrue();
    }

    [Fact]
    public async Task GetExternalUsersAsync_ReturnsTheEmailAddressOfEachUser()
    {
        SetupAwaitingInstructions();
        repository
            .Setup(repo => repo.GetExternalUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([User("a", true) with { EmailAddress = "jo.smith@example.test" }]);

        var result = await CreateService().GetExternalUsersAsync(
            pageNumber: 1, pageSize: 10, ExternalUserSortColumn.None, sortDescending: false, CancellationToken.None);

        result.Items.Should().ContainSingle().Which.EmailAddress.Should().Be("jo.smith@example.test");
    }

    [Theory]
    [InlineData(ExternalUserSortColumn.UserName, false, new[] { "a", "b", "c" })]
    [InlineData(ExternalUserSortColumn.UserName, true, new[] { "c", "b", "a" })]
    [InlineData(ExternalUserSortColumn.FullName, false, new[] { "a", "b", "c" })]
    [InlineData(ExternalUserSortColumn.Organisation, true, new[] { "c", "b", "a" })]
    [InlineData(ExternalUserSortColumn.EmailAddress, false, new[] { "a", "c", "b" })]
    [InlineData(ExternalUserSortColumn.EmailAddress, true, new[] { "b", "c", "a" })]
    [InlineData(ExternalUserSortColumn.None, true, new[] { "b", "c", "a" })]
    public async Task GetExternalUsersAsync_SortsBeforePaging(ExternalUserSortColumn column, bool descending, string[] expectedOrder)
    {
        SetupAwaitingInstructions();
        repository
            .Setup(repo => repo.GetExternalUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                User("b", false) with { Organisation = "Org B", EmailAddress = "z@example.test" },
                User("c", false) with { Organisation = "Org C", EmailAddress = "Y@example.test" },
                User("a", false) with { Organisation = "org a", EmailAddress = "x@example.test" }
            ]);

        var result = await CreateService().GetExternalUsersAsync(1, 0, column, descending, CancellationToken.None);

        result.Items.Select(user => user.UserName).Should().Equal(expectedOrder);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetReviewEmailSubscriptionAsync_SavesRequestedSubscription(bool subscribe)
    {
        var existing = User("a", !subscribe);
        ExternalUserAccount? saved = null;
        repository
            .Setup(repo => repo.GetExternalUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([existing]);
        repository
            .Setup(repo => repo.SaveExternalUserAsync(It.IsAny<ExternalUserAccount>(), It.IsAny<CancellationToken>()))
            .Callback<ExternalUserAccount, CancellationToken>((user, _) => saved = user)
            .Returns(Task.CompletedTask);

        var result = await CreateService().SetReviewEmailSubscriptionAsync(
            new SetExternalUserSubscriptionCommand(existing.Id, subscribe), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        saved.Should().BeEquivalentTo(existing with { SubscribedToReviewEmails = subscribe });
    }

    [Fact]
    public async Task PreRegisterExternalUserAsync_CreatesUserWithEmailAsUserName()
    {
        repository
            .Setup(repo => repo.PreRegisterExternalUserAsync(
                It.IsAny<Guid>(), "jane@example.test", "Jane Doe", "Org", "jane@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateService().PreRegisterExternalUserAsync(
            new PreRegisterExternalUserCommand(" Jane Doe ", " jane@example.test ", " Org "), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task PreRegisterExternalUserAsync_ReturnsConflict_WhenEmailAlreadyExists()
    {
        repository
            .Setup(repo => repo.PreRegisterExternalUserAsync(
                It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateService().PreRegisterExternalUserAsync(
            new PreRegisterExternalUserCommand("Jane Doe", "jane@example.test", "Org"), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Theory]
    [InlineData("", "jane@example.test", "Org")]
    [InlineData("Jane", "not-an-email", "Org")]
    [InlineData("Jane", "jane@example.test", "")]
    public void PreRegisterExternalUserCommandValidator_RejectsInvalidDetails(string fullName, string email, string organisation)
    {
        var result = new PreRegisterExternalUserCommandValidator().Validate(new PreRegisterExternalUserCommand(fullName, email, organisation));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task SetReviewEmailSubscriptionAsync_ReturnsNotFound_WhenUserDoesNotExist()
    {
        repository
            .Setup(repo => repo.GetExternalUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateService().SetReviewEmailSubscriptionAsync(
            new SetExternalUserSubscriptionCommand(Guid.NewGuid(), true), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetProfileAccessAsync_ReturnsNotFound_WhenUserIsNotExternal()
    {
        repository
            .Setup(repo => repo.GetExternalUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateService().GetProfileAccessAsync(Guid.NewGuid(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task GetProfileAccessAsync_MapsGrantsAndAvailableProfiles()
    {
        var existing = User("a", false);
        var sectionId = Guid.NewGuid();
        repository.Setup(repo => repo.GetExternalUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([existing]);
        repository
            .Setup(repo => repo.GetProfileAccessAsync(existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExternalUserProfileAccess
            {
                Profiles = [new ExternalUserProfileGrant { ProfileId = Guid.NewGuid(), Title = "T", RoleId = Guid.NewGuid(), RoleName = "Author", IsContributor = true, SectionPermissionIds = [sectionId] }],
                AvailableProfiles = [new ProfileOption { Id = Guid.NewGuid(), Title = "Other" }]
            });

        var result = await CreateService().GetProfileAccessAsync(existing.Id, CancellationToken.None);

        result.Value!.Profiles.Should().ContainSingle().Which.SectionPermissionIds.Should().Equal(sectionId);
        result.Value.AvailableProfiles.Should().ContainSingle().Which.Title.Should().Be("Other");
    }

    [Fact]
    public async Task SaveProfileAccessAsync_AddsContributor_WhenUserIsNotYetOnProfile()
    {
        var existing = User("a", false);
        var profileId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var sections = new List<Guid> { Guid.NewGuid() };
        AddContributorCommand? added = null;
        repository.Setup(repo => repo.GetExternalUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([existing]);
        contributors
            .Setup(service => service.GetContributorForEditAsync(profileId, existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContributorEditDto { Id = existing.Id, UserName = existing.UserName, FullName = existing.FullName, Organisation = existing.Organisation, IsSsoUser = true });
        contributors
            .Setup(service => service.AddContributorAsync(It.IsAny<AddContributorCommand>(), It.IsAny<CancellationToken>()))
            .Callback<AddContributorCommand, CancellationToken>((command, _) => added = command)
            .ReturnsAsync(Result.Success(Unit.Value));

        var result = await CreateService().SaveProfileAccessAsync(
            new SaveExternalUserProfileAccessCommand(existing.Id, profileId, roleId, sections), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        added.Should().NotBeNull();
        added!.IsSsoUser.Should().BeTrue();
        added.RoleId.Should().Be(roleId);
        added.SectionPermissionIds.Should().Equal(sections);
    }

    [Fact]
    public async Task SaveProfileAccessAsync_UpdatesContributor_WhenUserIsAlreadyOnProfile()
    {
        var existing = User("a", false);
        var profileId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        byte[] rowVersion = [1, 2, 3];
        UpdateContributorCommand? updated = null;
        repository.Setup(repo => repo.GetExternalUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([existing]);
        contributors
            .Setup(service => service.GetContributorForEditAsync(profileId, existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContributorEditDto { Id = existing.Id, UserName = existing.UserName, RoleId = Guid.NewGuid(), IsSsoUser = true, LastUpdated = rowVersion });
        contributors
            .Setup(service => service.UpdateContributorAsync(It.IsAny<UpdateContributorCommand>(), It.IsAny<CancellationToken>()))
            .Callback<UpdateContributorCommand, CancellationToken>((command, _) => updated = command)
            .ReturnsAsync(Result.Success(Unit.Value));

        var result = await CreateService().SaveProfileAccessAsync(
            new SaveExternalUserProfileAccessCommand(existing.Id, profileId, roleId, []), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        updated!.RoleId.Should().Be(roleId);
        updated.LastUpdated.Should().Equal(rowVersion);
    }

    [Fact]
    public async Task SaveProfileAccessAsync_ReturnsNotFound_WhenUserIsNotExternal()
    {
        repository.Setup(repo => repo.GetExternalUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await CreateService().SaveProfileAccessAsync(
            new SaveExternalUserProfileAccessCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), []), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task DeleteProfileAccessAsync_ReturnsNotFound_WhenUserHasNoAccessToProfile()
    {
        var existing = User("a", false);
        var profileId = Guid.NewGuid();
        repository.Setup(repo => repo.GetExternalUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([existing]);
        contributors
            .Setup(service => service.GetContributorForEditAsync(profileId, existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContributorEditDto { Id = existing.Id, IsSsoUser = true });

        var result = await CreateService().DeleteProfileAccessAsync(
            new DeleteExternalUserProfileAccessCommand(existing.Id, profileId), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task DeleteProfileAccessAsync_DeletesWithCurrentRowVersion()
    {
        var existing = User("a", false);
        var profileId = Guid.NewGuid();
        byte[] rowVersion = [9, 9];
        DeleteContributorCommand? deleted = null;
        repository.Setup(repo => repo.GetExternalUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([existing]);
        contributors
            .Setup(service => service.GetContributorForEditAsync(profileId, existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContributorEditDto { Id = existing.Id, RoleId = Guid.NewGuid(), IsSsoUser = true, LastUpdated = rowVersion });
        contributors
            .Setup(service => service.DeleteContributorAsync(It.IsAny<DeleteContributorCommand>(), It.IsAny<CancellationToken>()))
            .Callback<DeleteContributorCommand, CancellationToken>((command, _) => deleted = command)
            .ReturnsAsync(Result.Success(Unit.Value));

        var result = await CreateService().DeleteProfileAccessAsync(
            new DeleteExternalUserProfileAccessCommand(existing.Id, profileId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        deleted!.LastUpdated.Should().Equal(rowVersion);
    }

    [Fact]
    public async Task SaveProfileAccessAsync_ReturnsNotFound_WhenContributorCannotBeLoaded()
    {
        var existing = User("a", false);
        var profileId = Guid.NewGuid();
        repository.Setup(repo => repo.GetExternalUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([existing]);
        contributors
            .Setup(service => service.GetContributorForEditAsync(profileId, existing.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContributorEditDto?)null);

        var result = await CreateService().SaveProfileAccessAsync(
            new SaveExternalUserProfileAccessCommand(existing.Id, profileId, Guid.NewGuid(), []), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task DeleteProfileAccessAsync_ReturnsNotFound_WhenUserIsNotExternal()
    {
        repository.Setup(repo => repo.GetExternalUsersAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await CreateService().DeleteProfileAccessAsync(
            new DeleteExternalUserProfileAccessCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }
}
