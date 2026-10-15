using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ExternalUsers;
using CDC.Api.Features.ExternalUsers.Commands;
using CDC.Api.Features.ExternalUsers.Interfaces;
using CDC.Api.Features.ProfileContributors.Commands;
using CDC.Api.Features.ProfileContributors.Dtos;
using CDC.Api.Features.ProfileContributors.Interfaces;
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

    [Fact]
    public async Task GetExternalUsersAsync_ReturnsOnlyThatPage_WithSubscriptionState()
    {
        repository
            .Setup(repo => repo.GetExternalUsersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([User("a", true), User("b", false), User("c", true)]);

        var result = await CreateService().GetExternalUsersAsync(pageNumber: 2, pageSize: 2, CancellationToken.None);

        result.Items.Should().ContainSingle().Which.SubscribedToReviewEmails.Should().BeTrue();
        result.TotalRecords.Should().Be(3);
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
}
