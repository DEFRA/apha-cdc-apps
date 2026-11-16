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
                WhatIfScenarios = []
            }
        ];

        profileSearchService
            .Setup(service => service.GetProfileSearchResultsAsync("bovine", true, false, false, false, SearchForType.ExactWordOrPhrase, It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var response = await CreateController().SearchProfiles("bovine", true, false, false, false, SearchForType.ExactWordOrPhrase, CancellationToken.None);

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(results);
    }

    [Fact]
    public async Task SearchProfiles_PassesDisplayMyProfilesOnToTheService()
    {
        IReadOnlyList<ProfileSearchResultDto> results = [];

        profileSearchService
            .Setup(service => service.GetProfileSearchResultsAsync(null, true, true, true, true, SearchForType.AllWords, It.IsAny<CancellationToken>()))
            .ReturnsAsync(results);

        var response = await CreateController().SearchProfiles(null, true, true, true, true, SearchForType.AllWords, CancellationToken.None);

        response.Result.Should().BeOfType<OkObjectResult>().Which.Value.Should().BeSameAs(results);
        profileSearchService.VerifyAll();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetMyProfilesFilterPermission_ReturnsWhetherTheUserMayFilterToTheirOwnProfiles(bool canFilter)
    {
        profileSearchService.SetupGet(service => service.CanFilterToMyProfiles).Returns(canFilter);

        var response = CreateController().GetMyProfilesFilterPermission();

        var ok = response.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(new MyProfilesFilterPermissionDto { CanFilter = canFilter });
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
