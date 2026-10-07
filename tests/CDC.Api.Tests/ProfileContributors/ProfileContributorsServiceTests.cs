using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileContributors;
using CDC.Api.Features.ProfileContributors.Commands;
using CDC.Api.Features.ProfileContributors.Interfaces;
using CDC.Common.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ProfileContributors;

public class ProfileContributorsServiceTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();

    private readonly Mock<IProfileContributorsRepository> repository = new(MockBehavior.Strict);

    private ProfileContributorsService CreateService() => new(repository.Object, NullLogger<ProfileContributorsService>.Instance);

    private static Contributor Contributor(string userName) => new()
    {
        Id = Guid.NewGuid(),
        UserName = userName,
        FullName = $"{userName} full name",
        Organisation = "Pirbright Institute",
        Role = "Technical author",
        LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
    };

    [Fact]
    public async Task GetProfileContributorsAsync_ReturnsEveryContributor_WhenPageSizeIsZero()
    {
        repository
            .Setup(repo => repo.GetProfileContributorsAsync(ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Contributor("a"), Contributor("b"), Contributor("c")]);

        var result = await CreateService().GetProfileContributorsAsync(ProfileId, pageNumber: 1, pageSize: 0, CancellationToken.None);

        result.Items.Should().HaveCount(3);
        result.TotalRecords.Should().Be(3);
        result.TotalPages.Should().Be(1);
        repository.VerifyAll();
    }

    [Fact]
    public async Task GetProfileContributorsAsync_ReturnsOnlyThatPage_WhenPageSizeIsPositive()
    {
        repository
            .Setup(repo => repo.GetProfileContributorsAsync(ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Contributor("a"), Contributor("b"), Contributor("c")]);

        var result = await CreateService().GetProfileContributorsAsync(ProfileId, pageNumber: 2, pageSize: 2, CancellationToken.None);

        result.Items.Should().ContainSingle();
        result.Items[0].UserName.Should().Be("c");
        result.TotalRecords.Should().Be(3);
        result.TotalPages.Should().Be(2);
        result.PageNumber.Should().Be(2);
    }

    [Fact]
    public async Task GetProfileContributorsAsync_MapsEveryField()
    {
        var contributor = Contributor("carrie.batten");
        repository
            .Setup(repo => repo.GetProfileContributorsAsync(ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([contributor]);

        var result = await CreateService().GetProfileContributorsAsync(ProfileId, pageNumber: 1, pageSize: 10, CancellationToken.None);

        var dto = result.Items.Single();
        dto.Id.Should().Be(contributor.Id);
        dto.UserName.Should().Be(contributor.UserName);
        dto.FullName.Should().Be(contributor.FullName);
        dto.Organisation.Should().Be(contributor.Organisation);
        dto.Role.Should().Be(contributor.Role);
    }

    [Fact]
    public async Task GetProfileContributorsAsync_ClampsPageNumber_WhenItIsLessThanOne()
    {
        repository
            .Setup(repo => repo.GetProfileContributorsAsync(ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([Contributor("a")]);

        var result = await CreateService().GetProfileContributorsAsync(ProfileId, pageNumber: 0, pageSize: 10, CancellationToken.None);

        result.PageNumber.Should().Be(1);
    }

    private static readonly Guid ContributorId = Guid.NewGuid();
    private static readonly Guid RoleId = Guid.NewGuid();

    private static ContributorEdit ContributorEdit(bool isSsoUser = false, IReadOnlyList<Guid>? sectionPermissionIds = null) => new()
    {
        Id = ContributorId,
        UserName = "carrie.batten",
        FullName = "Carrie Batten",
        Organisation = "Pirbright Institute",
        RoleId = RoleId,
        IsSsoUser = isSsoUser,
        SectionPermissionIds = sectionPermissionIds ?? [],
        LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
    };

    private static ProfileUserRole ContributorRole() => new() { Id = RoleId, Name = "Technical author", IsContributor = true };

    private static ProfileUserRole ReviewerRole() => new() { Id = RoleId, Name = "Reviewer", IsContributor = false };

    private static UpdateContributorCommand Command(
        Guid? roleId = null,
        string fullName = "Carrie Batten",
        string organisation = "Pirbright Institute",
        IReadOnlyList<Guid>? sectionPermissionIds = null) => new(
            ProfileId,
            ContributorId,
            roleId ?? RoleId,
            fullName,
            organisation,
            sectionPermissionIds ?? [Guid.NewGuid()],
            [1, 2, 3, 4, 5, 6, 7, 8]);

    [Fact]
    public async Task GetContributorForEditAsync_ReturnsTheMappedContributor_WhenFound()
    {
        repository
            .Setup(repo => repo.GetContributorForEditAsync(ProfileId, ContributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContributorEdit());

        var result = await CreateService().GetContributorForEditAsync(ProfileId, ContributorId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.UserName.Should().Be("carrie.batten");
    }

    [Fact]
    public async Task GetContributorForEditAsync_ReturnsNull_WhenNotFound()
    {
        repository
            .Setup(repo => repo.GetContributorForEditAsync(ProfileId, ContributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CDC.Api.Domain.Entities.ContributorEdit?)null);

        var result = await CreateService().GetContributorForEditAsync(ProfileId, ContributorId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetProfileUserRolesAsync_ReturnsEveryMappedRole()
    {
        repository
            .Setup(repo => repo.GetProfileUserRolesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([ContributorRole(), ReviewerRole()]);

        var result = await CreateService().GetProfileUserRolesAsync(CancellationToken.None);

        result.Should().HaveCount(2);
        result[0].IsContributor.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateContributorAsync_ReturnsNotFound_WhenTheContributorDoesNotExist()
    {
        repository
            .Setup(repo => repo.GetContributorForEditAsync(ProfileId, ContributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CDC.Api.Domain.Entities.ContributorEdit?)null);

        var result = await CreateService().UpdateContributorAsync(Command(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task UpdateContributorAsync_ReturnsValidationFailed_WhenTheRoleDoesNotExist()
    {
        repository
            .Setup(repo => repo.GetContributorForEditAsync(ProfileId, ContributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContributorEdit());
        repository.Setup(repo => repo.GetProfileUserRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await CreateService().UpdateContributorAsync(Command(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.ValidationFailed);
        result.Error.Should().Be("Please select a valid role.");
    }

    [Fact]
    public async Task UpdateContributorAsync_ReturnsValidationFailed_WhenAContributorRoleHasNoSectionPermissions()
    {
        repository
            .Setup(repo => repo.GetContributorForEditAsync(ProfileId, ContributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContributorEdit());
        repository.Setup(repo => repo.GetProfileUserRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ContributorRole()]);

        var result = await CreateService().UpdateContributorAsync(Command(sectionPermissionIds: []), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.ValidationFailed);
        result.Error.Should().Be("A contributor must have at least one profile section permission granted.");
    }

    [Fact]
    public async Task UpdateContributorAsync_ReturnsValidationFailed_WhenAReviewerRoleHasSectionPermissions()
    {
        repository
            .Setup(repo => repo.GetContributorForEditAsync(ProfileId, ContributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContributorEdit());
        repository.Setup(repo => repo.GetProfileUserRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ReviewerRole()]);

        var result = await CreateService().UpdateContributorAsync(Command(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.ValidationFailed);
        result.Error.Should().Be("A reviewer must not have any profile section permissions granted.");
    }

    [Fact]
    public async Task UpdateContributorAsync_ReturnsValidationFailed_WhenANonSsoUserHasNoFullName()
    {
        repository
            .Setup(repo => repo.GetContributorForEditAsync(ProfileId, ContributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContributorEdit(isSsoUser: false));
        repository.Setup(repo => repo.GetProfileUserRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ReviewerRole()]);

        var result = await CreateService().UpdateContributorAsync(
            Command(fullName: "  ", sectionPermissionIds: []), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.ValidationFailed);
        result.Error.Should().Be("Please enter a full name.");
    }

    [Fact]
    public async Task UpdateContributorAsync_ReturnsValidationFailed_WhenANonSsoUserHasNoOrganisation()
    {
        repository
            .Setup(repo => repo.GetContributorForEditAsync(ProfileId, ContributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContributorEdit(isSsoUser: false));
        repository.Setup(repo => repo.GetProfileUserRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ReviewerRole()]);

        var result = await CreateService().UpdateContributorAsync(
            Command(organisation: string.Empty, sectionPermissionIds: []), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.ValidationFailed);
        result.Error.Should().Be("Please enter an organisation.");
    }

    [Fact]
    public async Task UpdateContributorAsync_SkipsFullNameAndOrganisationValidation_ForAnSsoUser()
    {
        repository
            .Setup(repo => repo.GetContributorForEditAsync(ProfileId, ContributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContributorEdit(isSsoUser: true));
        repository.Setup(repo => repo.GetProfileUserRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ReviewerRole()]);
        repository
            .Setup(repo => repo.UpdateContributorAsync(It.IsAny<UpdateContributorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([9, 9, 9, 9, 9, 9, 9, 9]);

        var result = await CreateService().UpdateContributorAsync(
            Command(fullName: string.Empty, organisation: string.Empty, sectionPermissionIds: []), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Success);
    }

    [Fact]
    public async Task UpdateContributorAsync_ReturnsSuccess_WhenTheUpdateSucceeds()
    {
        repository
            .Setup(repo => repo.GetContributorForEditAsync(ProfileId, ContributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContributorEdit());
        repository.Setup(repo => repo.GetProfileUserRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ContributorRole()]);
        repository
            .Setup(repo => repo.UpdateContributorAsync(It.IsAny<UpdateContributorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([9, 9, 9, 9, 9, 9, 9, 9]);

        var result = await CreateService().UpdateContributorAsync(Command(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Success);
    }

    [Fact]
    public async Task UpdateContributorAsync_ReturnsNotFound_WhenTheRepositoryFindsNoRowToUpdate()
    {
        repository
            .Setup(repo => repo.GetContributorForEditAsync(ProfileId, ContributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContributorEdit());
        repository.Setup(repo => repo.GetProfileUserRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ContributorRole()]);
        repository
            .Setup(repo => repo.UpdateContributorAsync(It.IsAny<UpdateContributorCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((byte[]?)null);

        var result = await CreateService().UpdateContributorAsync(Command(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task UpdateContributorAsync_ReturnsConflict_WhenTheRepositoryThrowsAConcurrencyException()
    {
        repository
            .Setup(repo => repo.GetContributorForEditAsync(ProfileId, ContributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ContributorEdit());
        repository.Setup(repo => repo.GetProfileUserRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ContributorRole()]);
        repository
            .Setup(repo => repo.UpdateContributorAsync(It.IsAny<UpdateContributorCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("edited by another user"));

        var result = await CreateService().UpdateContributorAsync(Command(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public async Task VerifyUsernameAsync_ReturnsBlocked_WhenTheUserIsAUserManagementSystemAccount()
    {
        repository
            .Setup(repo => repo.FindUserByUsernameAsync("internal\\admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserVerification { Id = Guid.NewGuid(), IsUserManagementSystem = true });

        var result = await CreateService().VerifyUsernameAsync("internal\\admin", CancellationToken.None);

        result.Outcome.Should().Be(UserVerificationOutcome.Blocked);
        result.UserId.Should().BeNull();
    }

    [Fact]
    public async Task VerifyUsernameAsync_ReturnsExistingUser_WhenTheUsernameMatchesAGlobalUser()
    {
        var userId = Guid.NewGuid();
        repository
            .Setup(repo => repo.FindUserByUsernameAsync("internal\\carrie.batten", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserVerification { Id = userId, IsUserManagementSystem = false });

        var result = await CreateService().VerifyUsernameAsync("internal\\carrie.batten", CancellationToken.None);

        result.Outcome.Should().Be(UserVerificationOutcome.ExistingUser);
        result.UserId.Should().Be(userId);
    }

    [Fact]
    public async Task VerifyUsernameAsync_ReturnsNewUser_WhenTheUsernameIsNotFoundButWellFormatted()
    {
        repository
            .Setup(repo => repo.FindUserByUsernameAsync("internal\\new.user", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserVerification?)null);

        var result = await CreateService().VerifyUsernameAsync("internal\\new.user", CancellationToken.None);

        result.Outcome.Should().Be(UserVerificationOutcome.NewUser);
    }

    [Fact]
    public async Task VerifyUsernameAsync_ReturnsInvalidFormat_WhenTheUsernameIsNotFoundAndNotWellFormatted()
    {
        repository
            .Setup(repo => repo.FindUserByUsernameAsync("not-a-valid-username", It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserVerification?)null);

        var result = await CreateService().VerifyUsernameAsync("not-a-valid-username", CancellationToken.None);

        result.Outcome.Should().Be(UserVerificationOutcome.InvalidFormat);
    }

    private static AddContributorCommand AddCommand(
        bool isSsoUser = false,
        Guid? roleId = null,
        string fullName = "Carrie Batten",
        string organisation = "Pirbright Institute",
        IReadOnlyList<Guid>? sectionPermissionIds = null) => new(
            ProfileId,
            ContributorId,
            "internal\\carrie.batten",
            isSsoUser,
            roleId ?? RoleId,
            fullName,
            organisation,
            sectionPermissionIds ?? [Guid.NewGuid()]);

    [Fact]
    public async Task AddContributorAsync_ReturnsValidationFailed_WhenTheRoleDoesNotExist()
    {
        repository.Setup(repo => repo.GetProfileUserRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var result = await CreateService().AddContributorAsync(AddCommand(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.ValidationFailed);
        result.Error.Should().Be("Please select a valid role.");
    }

    [Fact]
    public async Task AddContributorAsync_ReturnsValidationFailed_WhenAContributorRoleHasNoSectionPermissions()
    {
        repository.Setup(repo => repo.GetProfileUserRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ContributorRole()]);

        var result = await CreateService().AddContributorAsync(AddCommand(sectionPermissionIds: []), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.ValidationFailed);
        result.Error.Should().Be("A contributor must have at least one profile section permission granted.");
    }

    [Fact]
    public async Task AddContributorAsync_ReturnsValidationFailed_WhenAReviewerRoleHasSectionPermissions()
    {
        repository.Setup(repo => repo.GetProfileUserRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ReviewerRole()]);

        var result = await CreateService().AddContributorAsync(AddCommand(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.ValidationFailed);
        result.Error.Should().Be("A reviewer must not have any profile section permissions granted.");
    }

    [Fact]
    public async Task AddContributorAsync_ReturnsValidationFailed_WhenANonSsoUserHasNoFullName()
    {
        repository.Setup(repo => repo.GetProfileUserRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ContributorRole()]);

        var result = await CreateService().AddContributorAsync(AddCommand(fullName: " "), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.ValidationFailed);
        result.Error.Should().Be("Please enter a full name.");
    }

    [Fact]
    public async Task AddContributorAsync_SkipsFullNameValidation_ForAnSsoUser()
    {
        repository.Setup(repo => repo.GetProfileUserRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ContributorRole()]);
        repository
            .Setup(repo => repo.AddContributorAsync(It.IsAny<AddContributorCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateService().AddContributorAsync(AddCommand(isSsoUser: true, fullName: string.Empty), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Success);
    }

    [Fact]
    public async Task AddContributorAsync_ReturnsSuccess_WhenTheAddSucceeds()
    {
        repository.Setup(repo => repo.GetProfileUserRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ContributorRole()]);
        repository
            .Setup(repo => repo.AddContributorAsync(It.IsAny<AddContributorCommand>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateService().AddContributorAsync(AddCommand(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Success);
    }

    [Fact]
    public async Task AddContributorAsync_ReturnsConflict_WhenTheRepositoryThrowsAConcurrencyException()
    {
        repository.Setup(repo => repo.GetProfileUserRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ContributorRole()]);
        repository
            .Setup(repo => repo.AddContributorAsync(It.IsAny<AddContributorCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("edited by another user"));

        var result = await CreateService().AddContributorAsync(AddCommand(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public async Task AddContributorAsync_ReturnsValidationFailed_WhenTheRepositoryThrowsADuplicateUsernameException()
    {
        repository.Setup(repo => repo.GetProfileUserRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([ContributorRole()]);
        repository
            .Setup(repo => repo.AddContributorAsync(It.IsAny<AddContributorCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DuplicateUsernameException("There is already a user with the specified username."));

        var result = await CreateService().AddContributorAsync(AddCommand(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.ValidationFailed);
    }

    [Fact]
    public async Task DeleteContributorAsync_ReturnsSuccess_WhenTheDeleteSucceeds()
    {
        repository
            .Setup(repo => repo.DeleteContributorAsync(ProfileId, ContributorId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var command = new DeleteContributorCommand(ProfileId, ContributorId, [1, 2, 3, 4, 5, 6, 7, 8]);
        var result = await CreateService().DeleteContributorAsync(command, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Success);
    }

    [Fact]
    public async Task DeleteContributorAsync_ReturnsConflict_WhenTheRepositoryThrowsAConcurrencyException()
    {
        repository
            .Setup(repo => repo.DeleteContributorAsync(ProfileId, ContributorId, It.IsAny<byte[]>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("edited by another user"));

        var command = new DeleteContributorCommand(ProfileId, ContributorId, [1, 2, 3, 4, 5, 6, 7, 8]);
        var result = await CreateService().DeleteContributorAsync(command, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
    }
}
