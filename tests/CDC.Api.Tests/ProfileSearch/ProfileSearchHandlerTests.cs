using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileSearch;
using CDC.Api.Features.ProfileSearch.Dtos;
using CDC.Api.Features.ProfileSearch.Interfaces;
using CDC.Api.Features.ProfileSearch.Queries;
using FluentAssertions;
using Moq;

namespace CDC.Api.Tests.ProfileSearch;

public class ProfileSearchHandlerTests
{
    [Fact]
    public async Task GetAllProfilesQueryHandler_ReturnsSuccess()
    {
        IReadOnlyList<ProfileDto> profiles =
        [
            new() { Id = Guid.NewGuid(), Name = "Bovine tuberculosis", Status = "Published", IsActive = true }
        ];

        var service = new Mock<IProfileSearchService>();
        service
            .Setup(s => s.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(profiles);

        var result = await new GetAllProfilesQueryHandler(service.Object)
            .Handle(new GetAllProfilesQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(profiles);
    }

    [Fact]
    public async Task GetProfileVersionQueryHandler_ReturnsNotFound_WhenVersionIsMissing()
    {
        var profileVersionId = Guid.NewGuid();

        var service = new Mock<IProfileSearchService>();
        service
            .Setup(s => s.GetProfileVersionAsync(profileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionDto?)null);

        var result = await new GetProfileVersionQueryHandler(service.Object)
            .Handle(new GetProfileVersionQuery(profileVersionId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Error.Should().Contain(profileVersionId.ToString());
    }

    [Fact]
    public async Task GetProfileVersionQueryHandler_ReturnsSuccess_WhenVersionExists()
    {
        var profileVersionId = Guid.NewGuid();
        var version = new ProfileVersionDto
        {
            Id = profileVersionId,
            ProfileId = Guid.NewGuid(),
            VersionNumber = 2,
            Title = "Version 2",
            Content = "Details",
            IsPublished = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var service = new Mock<IProfileSearchService>();
        service
            .Setup(s => s.GetProfileVersionAsync(profileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(version);

        var result = await new GetProfileVersionQueryHandler(service.Object)
            .Handle(new GetProfileVersionQuery(profileVersionId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(version);
    }

    [Fact]
    public async Task ProfileSearchService_GetAllProfilesAsync_ReturnsProfiles()
    {
        var repository = new Mock<IProfileRepository>();
        repository
            .Setup(r => r.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileSearchResultDto>)
            [
                new()
                {
                    Id = Guid.NewGuid(),
                    Title = "Bovine tuberculosis",
                    Status = "Published",
                    CreatedAtUtc = DateTime.UtcNow,
                    ModifiedAtUtc = DateTime.UtcNow,
                    IsPublic = true,
                    AffectedSpecies = [],
                    PublishedVersions = [],
                    DraftVersions = [],
                    Scenarios = []
                }
            ]);

        var service = new ProfileSearchService(repository.Object);

        var profiles = await service.GetAllProfilesAsync(CancellationToken.None);

        profiles.Should().HaveCount(1);
        profiles[0].Name.Should().Be("Bovine tuberculosis");
    }

    [Fact]
    public async Task ProfileSearchService_GetProfileVersionAsync_ReturnsVersion()
    {
        var profileVersionId = Guid.NewGuid();
        var service = new ProfileSearchService(Mock.Of<IProfileRepository>());

        var version = await service.GetProfileVersionAsync(profileVersionId, CancellationToken.None);

        version.Should().NotBeNull();
        version!.Id.Should().Be(profileVersionId);
    }

    [Theory]
    [InlineData(true, false, false, "Published", true)]
    [InlineData(false, true, false, "Draft", true)]
    [InlineData(false, false, true, "Scenario", true)]
    [InlineData(false, false, false, "Published", false)]
    public async Task GetProfileSearchResultsAsync_FiltersByStatusFlags(
        bool displayPublished,
        bool displayDraft,
        bool displayScenarios,
        string profileStatus,
        bool expectedIncluded)
    {
        var repository = new Mock<IProfileRepository>();
        repository
            .Setup(r => r.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileSearchResultDto>) [CreateProfile("Bovine tuberculosis", profileStatus)]);

        var service = new ProfileSearchService(repository.Object);

        var results = await service.GetProfileSearchResultsAsync(
            displayPublished: displayPublished,
            displayDraft: displayDraft,
            displayScenarios: displayScenarios,
            cancellationToken: CancellationToken.None);

        results.Should().HaveCount(expectedIncluded ? 1 : 0);
    }

    [Fact]
    public async Task GetProfileSearchResultsAsync_FiltersByTitleContainingSearchText()
    {
        var repository = new Mock<IProfileRepository>();
        repository
            .Setup(r => r.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileSearchResultDto>)
            [
                CreateProfile("Bovine tuberculosis", "Published"),
                CreateProfile("Avian influenza", "Published")
            ]);

        var service = new ProfileSearchService(repository.Object);

        var results = await service.GetProfileSearchResultsAsync(searchText: "bovine", cancellationToken: CancellationToken.None);

        results.Should().ContainSingle().Which.Title.Should().Be("Bovine tuberculosis");
    }

    [Theory]
    [InlineData("All", 2)]
    [InlineData("B", 1)]
    [InlineData("Z", 0)]
    public async Task GetProfilesByLetterAsync_FiltersByStartingLetter(string letter, int expectedCount)
    {
        var repository = new Mock<IProfileRepository>();
        repository
            .Setup(r => r.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileSearchResultDto>)
            [
                CreateProfile("Bovine tuberculosis", "Published"),
                CreateProfile("Avian influenza", "Published")
            ]);

        var service = new ProfileSearchService(repository.Object);

        var results = await service.GetProfilesByLetterAsync(letter, CancellationToken.None);

        results.Should().HaveCount(expectedCount);
    }

    private static ProfileSearchResultDto CreateProfile(string title, string status) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Status = status,
        CreatedAtUtc = DateTime.UtcNow,
        ModifiedAtUtc = DateTime.UtcNow,
        IsPublic = true,
        AffectedSpecies = [],
        PublishedVersions = [],
        DraftVersions = [],
        Scenarios = []
    };
}
