using CDC.Api.Features.ProfileSearch;
using CDC.Api.Features.ProfileSearch.Dtos;
using CDC.Api.Features.ProfileSearch.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CDC.Api.Tests.ProfileSearch;

public class ProfileSearchControllerTests
{
    private readonly Mock<IProfileSearchService> profileSearchService = new(MockBehavior.Strict);

    private ProfileSearchController CreateController() => new(profileSearchService.Object);

    [Fact]
    public async Task SearchProfiles_ReturnsOk_WithResultsFromTheService()
    {
        IReadOnlyList<ProfileSearchResultDto> results =
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
        ];

        profileSearchService
            .Setup(service => service.GetProfileSearchResultsAsync("bovine", true, false, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var response = await CreateController().SearchProfiles("bovine", true, false, false, CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(results);
    }

    [Fact]
    public async Task SearchProfilesByLetter_ReturnsOk_WithResultsFromTheService()
    {
        IReadOnlyList<ProfileSearchResultDto> results = [];

        profileSearchService
            .Setup(service => service.GetProfilesByLetterAsync("B", It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var response = await CreateController().SearchProfilesByLetter("B", CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(results);
    }
}
