using CDC.Api.Application;
using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
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
                    WhatIfScenarios = []
                }
            ]);

        var service = new ProfileSearchService(repository.Object, EditorContext());

        var profiles = await service.GetAllProfilesAsync(CancellationToken.None);

        profiles.Should().HaveCount(1);
        profiles[0].Name.Should().Be("Bovine tuberculosis");
    }

    [Fact]
    public async Task ProfileSearchService_GetProfileVersionAsync_ReturnsVersion()
    {
        var profileVersionId = Guid.NewGuid();
        var service = new ProfileSearchService(Mock.Of<IProfileRepository>(), EditorContext());

        var version = await service.GetProfileVersionAsync(profileVersionId, CancellationToken.None);

        version.Should().NotBeNull();
        version!.Id.Should().Be(profileVersionId);
    }

    [Theory]
    [InlineData(true, false, false, "Published", true)]
    [InlineData(false, true, false, "Draft", true)]
    [InlineData(false, false, true, "Scenario", false)]
    [InlineData(false, false, false, "Published", false)]
    public async Task GetProfileSearchResultsAsync_FiltersByVersionAvailability(
        bool displayPublished,
        bool displayDraft,
        bool displayScenarios,
        string profileStatus,
        bool expectedIncluded)
    {
        var repository = new Mock<IProfileRepository>();
        repository
            .Setup(r => r.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileSearchResultDto>)[CreateProfile("Bovine tuberculosis", profileStatus)]);

        var service = new ProfileSearchService(repository.Object, EditorContext());

        var results = await service.GetProfileSearchResultsAsync(
            displayPublished: displayPublished,
            displayDraft: displayDraft,
            displayScenarios: displayScenarios,
            cancellationToken: CancellationToken.None);

        results.Should().HaveCount(expectedIncluded ? 1 : 0);
    }

    [Fact]
    public async Task GetProfileSearchResultsAsync_IncludesADraftAndPublishedProfile_WhenOnlyDisplayDraftIsOn()
    {
        var repository = new Mock<IProfileRepository>();
        repository
            .Setup(r => r.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileSearchResultDto>)[CreateProfile("Bovine tuberculosis", "Published", hasDraftVersionToo: true)]);

        var service = new ProfileSearchService(repository.Object, EditorContext());

        var results = await service.GetProfileSearchResultsAsync(
            displayPublished: false,
            displayDraft: true,
            cancellationToken: CancellationToken.None);

        results.Should().ContainSingle();
    }

    [Theory]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, true)]
    [InlineData(true, true, true, true)]
    [InlineData(false, true, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, false, true, false)]
    public async Task GetProfileSearchResultsAsync_MatchesOnAScenariosVersion_OnlyWhenThatVersionTypeIsDisplayed(
        bool displayPublished,
        bool displayDraft,
        bool displayScenarios,
        bool expectedIncluded)
    {
        // The profile's own lineage is empty; only its "what-if" scenario has a (draft) version.
        var repository = new Mock<IProfileRepository>();
        repository
            .Setup(r => r.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileSearchResultDto>)[CreateProfile("Bovine tuberculosis", "Scenario")]);

        var service = new ProfileSearchService(repository.Object, EditorContext());

        var results = await service.GetProfileSearchResultsAsync(
            displayPublished: displayPublished,
            displayDraft: displayDraft,
            displayScenarios: displayScenarios,
            cancellationToken: CancellationToken.None);

        results.Should().HaveCount(expectedIncluded ? 1 : 0);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task GetProfileSearchResultsAsync_ReturnsTheWhatIfScenarios_OnlyWhenDisplayScenariosIsOn(
        bool displayScenarios,
        int expectedScenarioCount)
    {
        var repository = new Mock<IProfileRepository>();
        repository
            .Setup(r => r.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileSearchResultDto>)[CreateProfile("Bovine tuberculosis", "Scenario")]);

        var service = new ProfileSearchService(repository.Object, EditorContext());

        var results = await service.GetProfileSearchResultsAsync(
            displayDraft: true,
            displayScenarios: displayScenarios,
            cancellationToken: CancellationToken.None);

        results.Should().ContainSingle().Which.WhatIfScenarios.Should().HaveCount(expectedScenarioCount);
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

        var service = new ProfileSearchService(repository.Object, EditorContext());

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

        var service = new ProfileSearchService(repository.Object, EditorContext());

        var results = await service.GetProfilesByLetterAsync(letter, CancellationToken.None);

        results.Should().HaveCount(expectedCount);
    }

    [Fact]
    public async Task GetProfileSearchResultsAsync_HidesTheDraftsOfAProfileAnExternalUserHasNoRoleOn()
    {
        var draftProfile = CreateProfile("Bovine tuberculosis", "Draft");
        var service = new ProfileSearchService(RepositoryReturning(draftProfile), ExternalContext());

        var results = await service.GetProfileSearchResultsAsync(
            displayPublished: false,
            displayDraft: true,
            cancellationToken: CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GetProfileSearchResultsAsync_ListsTheDraftsOfAProfileAnExternalUserHoldsARoleOn(bool isContributor)
    {
        var draftProfile = CreateProfile("Bovine tuberculosis", "Draft");
        var service = new ProfileSearchService(RepositoryReturning(draftProfile), ExternalContext(draftProfile.Id, isContributor));

        var results = await service.GetProfileSearchResultsAsync(
            displayPublished: false,
            displayDraft: true,
            cancellationToken: CancellationToken.None);

        results.Should().ContainSingle().Which.DraftVersions.Should().ContainSingle();
    }

    [Fact]
    public async Task GetProfileSearchResultsAsync_KeepsThePublishedVersions_WhileHidingTheDrafts_OfAnExternalUser()
    {
        var profile = CreateProfile("Bovine tuberculosis", "Published", hasDraftVersionToo: true) with
        {
            NextReviewDate = new DateTime(2027, 1, 1),
            ReviewIncomplete = true
        };
        var service = new ProfileSearchService(RepositoryReturning(profile), ExternalContext());

        var results = await service.GetProfileSearchResultsAsync(
            displayPublished: true,
            displayDraft: true,
            cancellationToken: CancellationToken.None);

        var result = results.Should().ContainSingle().Subject;
        result.PublishedVersions.Should().ContainSingle();
        result.DraftVersions.Should().BeEmpty();
        result.NextReviewDate.Should().BeNull();
        result.ReviewIncomplete.Should().BeFalse();
    }

    [Fact]
    public async Task GetProfileSearchResultsAsync_HidesTheDraftsOfAScenarioAnExternalUserHasNoRoleOn()
    {
        var scenarioProfile = CreateProfile("Bovine tuberculosis", "Scenario");
        var service = new ProfileSearchService(RepositoryReturning(scenarioProfile), ExternalContext(scenarioProfile.Id));

        var results = await service.GetProfileSearchResultsAsync(
            displayPublished: false,
            displayDraft: true,
            displayScenarios: true,
            cancellationToken: CancellationToken.None);

        results.Should().BeEmpty();
    }

    [Fact]
    public async Task GetProfileSearchResultsAsync_ListsTheDraftsOfAScenarioAnExternalUserHoldsARoleOn()
    {
        var scenarioProfile = CreateProfile("Bovine tuberculosis", "Scenario");
        var scenarioId = scenarioProfile.WhatIfScenarios[0].ScenarioId;
        var service = new ProfileSearchService(RepositoryReturning(scenarioProfile), ExternalContext(scenarioId));

        var results = await service.GetProfileSearchResultsAsync(
            displayPublished: false,
            displayDraft: true,
            displayScenarios: true,
            cancellationToken: CancellationToken.None);

        results.Should().ContainSingle().Which.WhatIfScenarios.Should().ContainSingle().Which.DraftVersions.Should().ContainSingle();
    }

    [Fact]
    public async Task GetProfileSearchResultsAsync_ListsEveryDraft_ForAPolicyProfileUser()
    {
        var draftProfile = CreateProfile("Bovine tuberculosis", "Draft");
        var policyUser = new Mock<IUserContext>();
        policyUser.SetupGet(context => context.IsPolicyProfileUser).Returns(true);
        var service = new ProfileSearchService(RepositoryReturning(draftProfile), policyUser.Object);

        var results = await service.GetProfileSearchResultsAsync(
            displayPublished: false,
            displayDraft: true,
            cancellationToken: CancellationToken.None);

        results.Should().ContainSingle();
    }

    [Fact]
    public async Task GetProfilesByLetterAsync_HidesTheDraftsOfAProfileAnExternalUserHasNoRoleOn()
    {
        var profile = CreateProfile("Bovine tuberculosis", "Published", hasDraftVersionToo: true);
        var service = new ProfileSearchService(RepositoryReturning(profile), ExternalContext());

        var results = await service.GetProfilesByLetterAsync("All", CancellationToken.None);

        results.Should().ContainSingle().Which.DraftVersions.Should().BeEmpty();
    }

    private static IUserContext EditorContext()
    {
        var user = new Mock<IUserContext>();
        user.SetupGet(context => context.IsProfileEditor).Returns(true);

        return user.Object;
    }

    /// <summary>An external user, who holds a role on the given profile or scenario when one is given.</summary>
    private static IUserContext ExternalContext(Guid? roleOnProfileId = null, bool isContributor = true)
    {
        var user = new Mock<IUserContext>();

        if (roleOnProfileId is { } profileId)
        {
            user
                .Setup(context => context.GetProfileRole(profileId))
                .Returns(new ProfileRoleAssignment(profileId, Guid.NewGuid(), isContributor ? "Technical author" : "Technical reviewer", isContributor));
        }

        return user.Object;
    }

    private static IProfileRepository RepositoryReturning(params ProfileSearchResultDto[] profiles)
    {
        var repository = new Mock<IProfileRepository>();
        repository
            .Setup(r => r.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileSearchResultDto>)profiles);

        return repository.Object;
    }

    private static ProfileSearchResultDto CreateProfile(string title, string status, bool hasDraftVersionToo = false) => new()
    {
        Id = Guid.NewGuid(),
        Title = title,
        Status = status,
        CreatedAtUtc = DateTime.UtcNow,
        ModifiedAtUtc = DateTime.UtcNow,
        IsPublic = true,
        AffectedSpecies = [],
        PublishedVersions = status == "Published" ? [CreateVersion()] : [],
        DraftVersions = status == "Draft" || hasDraftVersionToo ? [CreateVersion()] : [],
        WhatIfScenarios = status == "Scenario"
            ? [new ProfileScenarioDto { ScenarioId = Guid.NewGuid(), PublishedVersions = [], DraftVersions = [CreateVersion()] }]
            : []
    };

    private static ProfileHistoryItemDto CreateVersion() => new()
    {
        VersionId = Guid.NewGuid(),
        VersionNumber = 1,
        Title = "Title",
        CreatedAtUtc = DateTime.UtcNow,
        IsScenario = false
    };
}
