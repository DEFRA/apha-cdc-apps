using CDC.Api.Application;
using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileManagement;
using CDC.Api.Features.ProfileManagement.Commands;
using CDC.Api.Features.ProfileManagement.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ProfileManagement;

public class ProfileManagementServiceTests
{
    private readonly Mock<IProfileManagementRepository> repository = new(MockBehavior.Strict);

    private CDC.Api.Features.ProfileManagement.ProfileManagementService CreateService(IUserContext? userContext = null) =>
        new(
            repository.Object,
            userContext ?? ManageProfileLinkVisibilityTests.TestUserContext.ProfileEditor,
            NullLogger<CDC.Api.Features.ProfileManagement.ProfileManagementService>.Instance);

    [Fact]
    public async Task GetProfileAttributes_ReturnsNull_WhenRepositoryReturnsNull()
    {
        repository
            .Setup(repo => repo.GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Profile?)null);

        var result = await CreateService().GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetProfileAttributes_MapsProfileToDto()
    {
        repository
            .Setup(repo => repo.GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileManagementTestData.Profile());

        var result = await CreateService().GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().NotBeNull();
        result.Id.Should().Be(ProfileManagementTestData.ProfileId);
        result.Title.Should().Be("Bovine tuberculosis");
        result.AffectedSpecies.Should().ContainSingle();
        result.AffectedSpecies[0].SpeciesId.Should().Be(ProfileManagementTestData.SpeciesId);
    }

    [Fact]
    public async Task DeleteProfileVersion_ReturnsNull_WhenRepositoryReturnsNull()
    {
        repository
            .Setup(repo => repo.DeleteProfileVersionAsync(ProfileManagementTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeleteProfileVersionResult?)null);

        var result = await CreateService().DeleteProfileVersionAsync(ProfileManagementTestData.ProfileVersionId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProfileVersion_MapsResultToDto()
    {
        var repositoryResult = new DeleteProfileVersionResult { NextLatestProfileVersionId = Guid.NewGuid(), IsProfileDeleted = false };

        repository
            .Setup(repo => repo.DeleteProfileVersionAsync(ProfileManagementTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repositoryResult);

        var result = await CreateService().DeleteProfileVersionAsync(ProfileManagementTestData.ProfileVersionId, CancellationToken.None);

        result.Should().NotBeNull();
        result.NextLatestProfileVersionId.Should().Be(repositoryResult.NextLatestProfileVersionId);
        result.IsProfileDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task CreateNewProfileVersion_ReturnsNewProfileVersionId()
    {
        var command = new CreateNewProfileVersionCommand(ProfileManagementTestData.ProfileVersionId, IsPublished: true, IsPublic: false);

        repository
            .Setup(repo => repo.CreateNewProfileVersionAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileManagementTestData.NewProfileVersionId);

        var result = await CreateService().CreateNewProfileVersionAsync(command, CancellationToken.None);

        result.NewProfileVersionId.Should().Be(ProfileManagementTestData.NewProfileVersionId);
    }

    [Fact]
    public async Task GetAffectedSpecies_ReturnsNull_WhenRepositoryReturnsNull()
    {
        repository
            .Setup(repo => repo.GetAffectedSpeciesAsync(ProfileManagementTestData.SpeciesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AffectedSpeciesInfo?)null);

        var result = await CreateService().GetAffectedSpeciesAsync(ProfileManagementTestData.SpeciesId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetProfileStatusTypes_MapsEachStatus()
    {
        IReadOnlyList<ProfileStatusType> statusTypes =
        [
            new ProfileStatusType { Id = ProfileManagementTestData.ProfileStatusId, Name = "Draft", IsValidationComplete = false }
        ];

        repository
            .Setup(repo => repo.GetProfileStatusTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(statusTypes);

        var result = await CreateService().GetProfileStatusTypesAsync(CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(ProfileManagementTestData.ProfileStatusId);
        result[0].Name.Should().Be("Draft");
    }

    [Fact]
    public async Task SetProfileVersionPublicAccess_CallsRepository()
    {
        repository
            .Setup(repo => repo.SetProfileVersionPublicAccessAsync(ProfileManagementTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateService().SetProfileVersionPublicAccessAsync(ProfileManagementTestData.ProfileVersionId, CancellationToken.None);

        repository.VerifyAll();
    }

    [Fact]
    public async Task UpdateProfileStatus_CallsRepository()
    {
        repository
            .Setup(repo => repo.UpdateProfileStatusAsync(
                ProfileManagementTestData.ProfileId,
                ProfileManagementTestData.ProfileStatusId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await CreateService().UpdateProfileStatusAsync(
            ProfileManagementTestData.ProfileId,
            ProfileManagementTestData.ProfileStatusId,
            CancellationToken.None);

        repository.VerifyAll();
    }

    [Fact]
    public async Task CreateProfile_MapsResultToDto()
    {
        var command = new CreateProfileCommand
        {
            Id = ProfileManagementTestData.ProfileId,
            CurrentDraftProfileVersionId = ProfileManagementTestData.ProfileVersionId,
            CloneProfileVersionId = Guid.Empty,
            ParentId = Guid.Empty,
            ProfileStatusId = ProfileManagementTestData.ProfileStatusId
        };

        var repositoryResult = new ProfileCreationResult
        {
            NewProfileId = ProfileManagementTestData.ProfileId,
            NewLastUpdated = ProfileManagementTestData.RowVersion
        };

        repository
            .Setup(repo => repo.CreateProfileAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(repositoryResult);

        var result = await CreateService().CreateProfileAsync(command, CancellationToken.None);

        result.NewProfileId.Should().Be(ProfileManagementTestData.ProfileId);
        result.NewLastUpdated.Should().BeEquivalentTo(ProfileManagementTestData.RowVersion);
    }

    [Fact]
    public async Task UpdateProfileAttributes_ReturnsNewLastUpdated()
    {
        var command = new UpdateProfileAttributesCommand
        {
            Id = ProfileManagementTestData.ProfileId,
            Title = "Bovine tuberculosis",
            LastUpdated = ProfileManagementTestData.RowVersion
        };

        repository
            .Setup(repo => repo.UpdateProfileAttributesAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileManagementTestData.NewRowVersion);

        var result = await CreateService().UpdateProfileAttributesAsync(command, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.NewLastUpdated.Should().BeEquivalentTo(ProfileManagementTestData.NewRowVersion);
    }

    // The legacy Profile.CanEditProfile(): a profile editor who is not a user management system account.
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, false)]
    public async Task UpdateProfileAttributes_IsForbidden_WithoutTouchingTheProfile_WhenTheUserMayNotEditAProfile(
        bool isProfileEditor, bool isUserManagementSystem, bool isPolicyProfileUser)
    {
        var command = new UpdateProfileAttributesCommand
        {
            Id = ProfileManagementTestData.ProfileId,
            Title = "Bovine tuberculosis",
            LastUpdated = ProfileManagementTestData.RowVersion
        };
        var userContext = new ManageProfileLinkVisibilityTests.TestUserContext(isProfileEditor, isUserManagementSystem, isPolicyProfileUser);

        var result = await CreateService(userContext).UpdateProfileAttributesAsync(command, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
        result.Error.Should().Be(CDC.Api.Features.ProfileManagement.ProfileManagementService.EditNotPermittedMessage);
        repository.Verify(
            repo => repo.UpdateProfileAttributesAsync(It.IsAny<UpdateProfileAttributesCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetNewProfileDefaults_ReturnsNull_WhenRepositoryReturnsNull()
    {
        repository
            .Setup(repo => repo.GetNewProfileDefaultsAsync(ProfileManagementTestData.ProfileVersionId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((NewProfileDefaults?)null);

        var result = await CreateService().GetNewProfileDefaultsAsync(ProfileManagementTestData.ProfileVersionId, false, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetNewProfileDefaults_MapsDefaultsToDto()
    {
        var defaults = new NewProfileDefaults
        {
            Title = "Bovine tuberculosis",
            ScenarioTitle = string.Empty,
            ParentId = Guid.Empty,
            ParentTitle = string.Empty,
            ProfileStatusId = ProfileManagementTestData.ProfileStatusId,
            AffectedSpecies = [ProfileManagementTestData.AffectedSpecies()],
            ClonedVersionDescription = "Bovine tuberculosis - v1.0 (Published) Valid from 01/01/2020",
            ClonedVersionIsWhatIfScenario = true
        };

        repository
            .Setup(repo => repo.GetNewProfileDefaultsAsync(ProfileManagementTestData.ProfileVersionId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(defaults);

        var result = await CreateService().GetNewProfileDefaultsAsync(ProfileManagementTestData.ProfileVersionId, true, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Title.Should().Be("Bovine tuberculosis");
        result.ProfileStatusId.Should().Be(ProfileManagementTestData.ProfileStatusId);
        result.ClonedVersionDescription.Should().Be("Bovine tuberculosis - v1.0 (Published) Valid from 01/01/2020");
        result.ClonedVersionIsWhatIfScenario.Should().BeTrue();
        result.AffectedSpecies.Should().ContainSingle();
        result.AffectedSpecies[0].SpeciesId.Should().Be(ProfileManagementTestData.SpeciesId);
    }

    [Fact]
    public async Task GetManageProfile_ReturnsNull_WhenRepositoryReturnsNull()
    {
        repository
            .Setup(repo => repo.GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Profile?)null);

        var result = await CreateService().GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetManageProfile_FormatsVersionLabelsAndResolvesStatusName()
    {
        var publishedVersionId = Guid.NewGuid();
        var publicVersionId = Guid.NewGuid();
        var profile = ProfileManagementTestData.Profile() with
        {
            CurrentDraftProfileVersionId = ProfileManagementTestData.ProfileVersionId,
            CurrentPublishedProfileVersionId = publishedVersionId,
            CurrentPublicVersionId = publicVersionId
        };

        repository
            .Setup(repo => repo.GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionSummary?)null);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(ProfileManagementTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileVersionSummary { VersionMajor = 8, VersionMinor = 0 });
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(publishedVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileVersionSummary { VersionMajor = 7, VersionMinor = 0 });
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(publicVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileVersionSummary { VersionMajor = 5, VersionMinor = 0 });
        repository
            .Setup(repo => repo.GetProfileStatusTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileStatusType>)[
                new ProfileStatusType { Id = ProfileManagementTestData.ProfileStatusId, Name = "Draft", IsValidationComplete = false }
            ]);

        var result = await CreateService().GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().NotBeNull();
        result.ProfileTitle.Should().Be("Bovine tuberculosis");
        result.ScenarioTitle.Should().Be("Current situation");
        result.IsWhatIfScenario.Should().BeFalse();
        result.FullTitle.Should().Be("Bovine tuberculosis");
        result.LatestDraftVersion.Should().Be("8.0");
        result.LatestPublishedVersionDefraNetOnly.Should().Be("7.0");
        result.LatestPublishedVersionPublic.Should().Be("5.0");
        result.ProfileStatus.Should().Be("Draft");
        result.ProfileStatusId.Should().Be(ProfileManagementTestData.ProfileStatusId);
        result.CurrentProfileVersionId.Should().Be(ProfileManagementTestData.ProfileVersionId);
        result.NewDraftVersionLabel.Should().Be("8.1");
    }

    [Fact]
    public async Task GetManageProfile_PopulatesCurrentVersionDisplayFields_FromTheCurrentVersionSummary()
    {
        var effectiveDateFrom = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc);
        var profile = ProfileManagementTestData.Profile() with
        {
            CurrentDraftProfileVersionId = Guid.Empty,
            CurrentPublishedProfileVersionId = ProfileManagementTestData.ProfileVersionId,
            CurrentPublicVersionId = Guid.Empty
        };

        repository
            .Setup(repo => repo.GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionSummary?)null);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(ProfileManagementTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileVersionSummary
            {
                VersionMajor = 13,
                VersionMinor = 0,
                IsPublished = true,
                EffectiveDateFrom = effectiveDateFrom
            });
        repository
            .Setup(repo => repo.GetProfileStatusTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileStatusType>)[]);

        var result = await CreateService().GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().NotBeNull();
        result.CurrentVersionNumber.Should().Be("13.0");
        result.CurrentVersionIsPublished.Should().BeTrue();
        result.CurrentVersionValidFrom.Should().Be(effectiveDateFrom);
        result.CurrentVersionDisplay.Should().Be("Version 13.0 (Published) Valid from 11/09/2026");
        result.CanPrintProfileSection.Should().BeFalse();
        result.CanViewProfileReports.Should().BeFalse();
    }

    [Fact]
    public async Task GetManageProfile_AllowsPrintingAndViewingReports_ForACurrentDraftVersion_WhenUserIsProfileEditor()
    {
        var profile = ProfileManagementTestData.Profile() with
        {
            CurrentDraftProfileVersionId = ProfileManagementTestData.ProfileVersionId,
            CurrentPublishedProfileVersionId = Guid.Empty,
            CurrentPublicVersionId = Guid.Empty
        };

        repository
            .Setup(repo => repo.GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionSummary?)null);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(ProfileManagementTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileVersionSummary { VersionMajor = 11, VersionMinor = 5, IsPublished = false });
        repository
            .Setup(repo => repo.GetProfileStatusTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileStatusType>)[]);

        var result = await CreateService().GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().NotBeNull();
        result.CanPrintProfileSection.Should().BeTrue();
        result.CanViewProfileReports.Should().BeTrue();
        result.CanEditProfileSection.Should().BeTrue();
        result.CanClearProfileSection.Should().BeTrue();
        result.CanViewReviewComments.Should().BeTrue();
        result.CanViewContributions.Should().BeTrue();
        result.CanViewAllRevisionDates.Should().BeTrue();
    }

    [Fact]
    public async Task GetManageProfile_DeniesEditingAndClearing_ForACurrentDraftVersion_WhenUserIsAUserManagementSystemAccount()
    {
        var profile = ProfileManagementTestData.Profile() with
        {
            CurrentDraftProfileVersionId = ProfileManagementTestData.ProfileVersionId,
            CurrentPublishedProfileVersionId = Guid.Empty,
            CurrentPublicVersionId = Guid.Empty
        };

        repository
            .Setup(repo => repo.GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionSummary?)null);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(ProfileManagementTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileVersionSummary { VersionMajor = 11, VersionMinor = 5, IsPublished = false });
        repository
            .Setup(repo => repo.GetProfileStatusTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileStatusType>)[]);

        var result = await CreateService(ManageProfileLinkVisibilityTests.TestUserContext.UserManagementSystem)
            .GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().NotBeNull();
        result.CanEditProfileSection.Should().BeFalse();
        result.CanClearProfileSection.Should().BeFalse();
        result.CanViewReviewComments.Should().BeFalse();
        result.CanViewContributions.Should().BeFalse();
        result.CanViewAllRevisionDates.Should().BeFalse();
    }

    [Fact]
    public async Task GetManageProfile_DeniesPrintingAndViewingReports_ForACurrentDraftVersion_WhenUserIsReadOnly()
    {
        var profile = ProfileManagementTestData.Profile() with
        {
            CurrentDraftProfileVersionId = ProfileManagementTestData.ProfileVersionId,
            CurrentPublishedProfileVersionId = Guid.Empty,
            CurrentPublicVersionId = Guid.Empty
        };

        repository
            .Setup(repo => repo.GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionSummary?)null);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(ProfileManagementTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileVersionSummary { VersionMajor = 11, VersionMinor = 5, IsPublished = false });
        repository
            .Setup(repo => repo.GetProfileStatusTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileStatusType>)[]);

        var result = await CreateService(ManageProfileLinkVisibilityTests.TestUserContext.ReadOnly)
            .GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().NotBeNull();
        result.CanPrintProfileSection.Should().BeFalse();
        result.CanViewProfileReports.Should().BeFalse();
    }

    [Fact]
    public async Task GetManageProfile_AllowsPrinting_ButNotViewingReports_ForADraftVersionWithAnEffectiveToDate()
    {
        var profile = ProfileManagementTestData.Profile() with
        {
            CurrentDraftProfileVersionId = ProfileManagementTestData.ProfileVersionId,
            CurrentPublishedProfileVersionId = Guid.Empty,
            CurrentPublicVersionId = Guid.Empty
        };

        repository
            .Setup(repo => repo.GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionSummary?)null);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(ProfileManagementTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileVersionSummary
            {
                VersionMajor = 12,
                VersionMinor = 2,
                IsPublished = false,
                EffectiveDateTo = new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc)
            });
        repository
            .Setup(repo => repo.GetProfileStatusTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileStatusType>)[]);

        var result = await CreateService().GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().NotBeNull();
        result.CanPrintProfileSection.Should().BeTrue();
        result.CanViewProfileReports.Should().BeFalse();
    }

    [Fact]
    public async Task GetManageProfile_NewDraftVersionLabel_IsBasedOnThePublishedVersion_WhenThereIsNoDraft()
    {
        var publishedVersionId = Guid.NewGuid();
        var profile = ProfileManagementTestData.Profile() with
        {
            CurrentDraftProfileVersionId = Guid.Empty,
            CurrentPublishedProfileVersionId = publishedVersionId,
            CurrentPublicVersionId = Guid.Empty
        };

        repository
            .Setup(repo => repo.GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionSummary?)null);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(publishedVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileVersionSummary { VersionMajor = 13, VersionMinor = 0 });
        repository
            .Setup(repo => repo.GetProfileStatusTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileStatusType>)[]);

        var result = await CreateService().GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().NotBeNull();
        result.NewDraftVersionLabel.Should().Be("13.1");
    }

    [Fact]
    public async Task GetManageProfile_NewDraftVersionLabel_IsEmpty_WhenThereIsNoDraftOrPublishedVersion()
    {
        var profile = ProfileManagementTestData.Profile() with
        {
            CurrentDraftProfileVersionId = Guid.Empty,
            CurrentPublishedProfileVersionId = Guid.Empty,
            CurrentPublicVersionId = Guid.Empty
        };

        repository
            .Setup(repo => repo.GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionSummary?)null);
        repository
            .Setup(repo => repo.GetProfileStatusTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileStatusType>)[]);

        var result = await CreateService().GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().NotBeNull();
        result.NewDraftVersionLabel.Should().BeEmpty();
    }

    [Fact]
    public async Task GetManageProfile_PrefersPublishedThenPublicVersion_WhenNoDraftExists()
    {
        var publishedVersionId = Guid.NewGuid();
        var publicVersionId = Guid.NewGuid();
        var profile = ProfileManagementTestData.Profile() with
        {
            CurrentDraftProfileVersionId = Guid.Empty,
            CurrentPublishedProfileVersionId = publishedVersionId,
            CurrentPublicVersionId = publicVersionId
        };

        repository
            .Setup(repo => repo.GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionSummary?)null);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(publishedVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileVersionSummary { VersionMajor = 7, VersionMinor = 0 });
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(publicVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionSummary?)null);
        repository
            .Setup(repo => repo.GetProfileStatusTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileStatusType>)[]);

        var result = await CreateService().GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().NotBeNull();
        result.CurrentProfileVersionId.Should().Be(publishedVersionId);
    }

    [Fact]
    public async Task GetManageProfile_ReturnsNoneVersionLabels_WhenNoVersionIsSet()
    {
        var profile = ProfileManagementTestData.Profile() with
        {
            CurrentDraftProfileVersionId = Guid.Empty,
            CurrentPublishedProfileVersionId = Guid.Empty,
            CurrentPublicVersionId = Guid.Empty
        };

        repository
            .Setup(repo => repo.GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionSummary?)null);
        repository
            .Setup(repo => repo.GetProfileStatusTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileStatusType>)[]);

        var result = await CreateService().GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().NotBeNull();
        result.LatestDraftVersion.Should().Be("- none -");
        result.LatestPublishedVersionDefraNetOnly.Should().Be("- none -");
        result.LatestPublishedVersionPublic.Should().Be("- none -");
        result.ProfileStatus.Should().BeEmpty();
    }

    [Fact]
    public async Task GetManageProfile_ReturnsTheScenarioTitle_WhenTheProfileIsAWhatIfScenario()
    {
        var profile = ProfileManagementTestData.Profile() with
        {
            ParentId = Guid.NewGuid(),
            ScenarioTitle = "Low uptake scenario",
            CurrentDraftProfileVersionId = Guid.Empty,
            CurrentPublishedProfileVersionId = Guid.Empty,
            CurrentPublicVersionId = Guid.Empty
        };

        repository
            .Setup(repo => repo.GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        repository
            .Setup(repo => repo.GetProfileAttributesAsync(profile.ParentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Profile?)null);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionSummary?)null);
        repository
            .Setup(repo => repo.GetProfileStatusTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileStatusType>)[]);

        var result = await CreateService().GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().NotBeNull();
        result.ScenarioTitle.Should().Be("Low uptake scenario");
        result.IsWhatIfScenario.Should().BeTrue();
        result.FullTitle.Should().Be($"{result.ProfileTitle} (Low uptake scenario)");
    }

    [Fact]
    public async Task GetManageProfile_LetsTheProfileEditorEditEverySectionAndEveryReviewAndWriteReviewComments()
    {
        GivenACurrentDraft();

        var result = await CreateService().GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().NotBeNull();
        result.CanEditProfileSection.Should().BeTrue();
        result.CanEditAllSections.Should().BeTrue();
        result.EditableSectionIds.Should().BeEmpty();
        result.CanEditAuthorReviewDates.Should().BeTrue();
        result.CanEditVaReviewDates.Should().BeTrue();
        result.CanEditPolicyReviewDates.Should().BeTrue();
        result.CanClearProfileSection.Should().BeTrue();
        result.CanAddReviewComments.Should().BeTrue();
    }

    [Fact]
    public async Task GetManageProfile_LetsAnAuthorEditOnlyTheirSectionsAndWriteCommentsButNotChangeTheReviewDates()
    {
        GivenACurrentDraft();
        var sectionId = Guid.NewGuid();

        var result = await CreateService(ExternalUser("VA author", isContributor: true, sectionId).Object)
            .GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().NotBeNull();
        result.CanEditProfileSection.Should().BeTrue();
        result.CanEditAllSections.Should().BeFalse();
        result.EditableSectionIds.Should().Equal(sectionId);
        result.CanEditAuthorReviewDates.Should().BeFalse();
        result.CanEditVaReviewDates.Should().BeFalse();
        result.CanEditPolicyReviewDates.Should().BeFalse();
        result.CanClearProfileSection.Should().BeFalse();
        result.CanAddReviewComments.Should().BeTrue();
        AssertTheDraftCanBeReadAndReportedOn(result);
    }

    [Fact]
    public async Task GetManageProfile_LetsAnAuthorWithNoAssignedSectionReadButNotEdit()
    {
        GivenACurrentDraft();

        var result = await CreateService(ExternalUser("Technical author", isContributor: true).Object)
            .GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().NotBeNull();
        result.CanEditProfileSection.Should().BeFalse();
        result.EditableSectionIds.Should().BeEmpty();
        result.CanEditAuthorReviewDates.Should().BeFalse();
        AssertTheDraftCanBeReadAndReportedOn(result);
    }

    [Fact]
    public async Task GetManageProfile_LetsAReviewerReadTheDraftAndWriteCommentsButChangeNothingElse()
    {
        GivenACurrentDraft();

        var result = await CreateService(ExternalUser("Policy reviewer", isContributor: false, Guid.NewGuid()).Object)
            .GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().NotBeNull();
        result.CanEditProfileSection.Should().BeFalse();
        result.CanEditAllSections.Should().BeFalse();
        result.EditableSectionIds.Should().BeEmpty();
        result.CanEditAuthorReviewDates.Should().BeFalse();
        result.CanEditVaReviewDates.Should().BeFalse();
        result.CanEditPolicyReviewDates.Should().BeFalse();
        result.CanClearProfileSection.Should().BeFalse();
        result.CanAddReviewComments.Should().BeTrue();
        AssertTheDraftCanBeReadAndReportedOn(result);
    }

    [Fact]
    public async Task GetManageProfile_LetsThePolicyProfileUserReadTheDraftButChangeNothing()
    {
        GivenACurrentDraft();
        var policyUser = new Mock<IUserContext>();
        policyUser.SetupGet(context => context.IsPolicyProfileUser).Returns(true);

        var result = await CreateService(policyUser.Object).GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().NotBeNull();
        result.CanEditProfileSection.Should().BeFalse();
        result.CanEditAuthorReviewDates.Should().BeFalse();
        result.CanEditVaReviewDates.Should().BeFalse();
        result.CanEditPolicyReviewDates.Should().BeFalse();
        result.CanClearProfileSection.Should().BeFalse();
        result.CanAddReviewComments.Should().BeFalse();
        AssertTheDraftCanBeReadAndReportedOn(result);
    }

    [Fact]
    public async Task GetManageProfile_HidesTheDraft_FromAUserWithNoRoleOnTheProfile()
    {
        GivenACurrentDraft();
        var noRole = new Mock<IUserContext>();

        var result = await CreateService(noRole.Object).GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().NotBeNull();
        result.LatestDraftVersion.Should().Be("- none -");
        result.CurrentProfileVersionId.Should().Be(Guid.Empty);
        result.CanPrintProfileSection.Should().BeFalse();
        result.CanViewProfileReports.Should().BeFalse();
        result.CanEditProfileSection.Should().BeFalse();
        result.CanViewNotes.Should().BeFalse();
        result.CanViewReviewComments.Should().BeFalse();
        result.CanViewContributions.Should().BeFalse();
        result.CanViewAllRevisionDates.Should().BeFalse();
    }

    [Fact]
    public async Task GetManageProfile_LetsAnyoneWhoCanReadTheDraftSeeTheNotes_EvenWhenTheCurrentVersionIsPublished()
    {
        GivenACurrentDraft(isPublished: true);
        var policyUser = new Mock<IUserContext>();
        policyUser.SetupGet(context => context.IsPolicyProfileUser).Returns(true);

        var result = await CreateService(policyUser.Object).GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().NotBeNull();
        result.CanViewNotes.Should().BeTrue();
        result.CanViewReviewComments.Should().BeFalse();
    }

    [Fact]
    public async Task GetManageProfile_LetsNobodyEditAPublishedVersion_NotEvenTheProfileEditor()
    {
        GivenACurrentDraft(isPublished: true);

        var result = await CreateService().GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        result.Should().NotBeNull();
        result.CurrentVersionIsPublished.Should().BeTrue();
        result.CanEditProfileSection.Should().BeFalse();
        result.CanEditAllSections.Should().BeFalse();
        result.EditableSectionIds.Should().BeEmpty();
        result.CanEditAuthorReviewDates.Should().BeFalse();
        result.CanClearProfileSection.Should().BeFalse();
        result.CanAddReviewComments.Should().BeFalse();
    }

    private static void AssertTheDraftCanBeReadAndReportedOn(CDC.Common.Contracts.ManageProfileContract result)
    {
        result.CanPrintProfileSection.Should().BeTrue();
        result.CanViewProfileReports.Should().BeTrue();
        result.CanViewNotes.Should().BeTrue();
        result.CanViewReviewComments.Should().BeTrue();
        result.CanViewContributions.Should().BeTrue();
        result.CanViewAllRevisionDates.Should().BeTrue();
    }

    private static Mock<IUserContext> ExternalUser(string roleName, bool isContributor, params Guid[] assignedSectionIds)
    {
        var user = new Mock<IUserContext>();
        user
            .Setup(context => context.GetProfileRole(ProfileManagementTestData.ProfileId))
            .Returns(new ProfileRoleAssignment(ProfileManagementTestData.ProfileId, Guid.NewGuid(), roleName, isContributor));
        user
            .Setup(context => context.GetAssignedSectionIds(ProfileManagementTestData.ProfileId))
            .Returns(isContributor ? assignedSectionIds : []);

        return user;
    }

    /// <summary>A profile whose current version is a draft, or a published version when <paramref name="isPublished"/> is set.</summary>
    private void GivenACurrentDraft(bool isPublished = false)
    {
        var profile = ProfileManagementTestData.Profile() with
        {
            CurrentDraftProfileVersionId = isPublished ? Guid.Empty : ProfileManagementTestData.ProfileVersionId,
            CurrentPublishedProfileVersionId = isPublished ? ProfileManagementTestData.ProfileVersionId : Guid.Empty,
            CurrentPublicVersionId = Guid.Empty
        };

        repository
            .Setup(repo => repo.GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(Guid.Empty, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionSummary?)null);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(ProfileManagementTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileVersionSummary { VersionMajor = 3, VersionMinor = 1, IsPublished = isPublished });
        repository
            .Setup(repo => repo.GetProfileStatusTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileStatusType>)[]);
    }
}
