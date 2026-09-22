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
        var service = new ProfileSearchService();

        var profiles = await service.GetAllProfilesAsync(CancellationToken.None);

        profiles.Should().HaveCount(2);
        profiles[0].Name.Should().Be("Bovine tuberculosis");
    }

    [Fact]
    public async Task ProfileSearchService_GetProfileVersionAsync_ReturnsVersion()
    {
        var profileVersionId = Guid.NewGuid();
        var service = new ProfileSearchService();

        var version = await service.GetProfileVersionAsync(profileVersionId, CancellationToken.None);

        version.Should().NotBeNull();
        version!.Id.Should().Be(profileVersionId);
    }
}
