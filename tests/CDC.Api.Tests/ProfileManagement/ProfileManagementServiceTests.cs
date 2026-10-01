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

    private CDC.Api.Features.ProfileManagement.ProfileManagementService CreateService() =>
        new(repository.Object, NullLogger<CDC.Api.Features.ProfileManagement.ProfileManagementService>.Instance);

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
        result!.Id.Should().Be(ProfileManagementTestData.ProfileId);
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
        result!.NextLatestProfileVersionId.Should().Be(repositoryResult.NextLatestProfileVersionId);
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
        result!.ProfileTitle.Should().Be("Bovine tuberculosis");
        result.LatestDraftVersion.Should().Be("Version 8.0");
        result.LatestPublishedVersionDefraNetOnly.Should().Be("Version 7.0");
        result.LatestPublishedVersionPublic.Should().Be("Version 5.0");
        result.ProfileStatus.Should().Be("Draft");
        result.ProfileStatusId.Should().Be(ProfileManagementTestData.ProfileStatusId);
    }

    [Fact]
    public async Task GetManageProfile_ReturnsEmptyVersionLabels_WhenNoVersionIsSet()
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
        result!.LatestDraftVersion.Should().BeEmpty();
        result.LatestPublishedVersionDefraNetOnly.Should().BeEmpty();
        result.LatestPublishedVersionPublic.Should().BeEmpty();
        result.ProfileStatus.Should().BeEmpty();
    }
}
