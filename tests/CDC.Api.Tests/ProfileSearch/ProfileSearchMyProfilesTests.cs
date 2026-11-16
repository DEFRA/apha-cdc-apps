using CDC.Api.Application;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileSearch;
using CDC.Api.Features.ProfileSearch.Dtos;
using CDC.Api.Features.ProfileSearch.Interfaces;
using FluentAssertions;
using Moq;

namespace CDC.Api.Tests.ProfileSearch;

// "Display my own profiles only" on the Search page. As legacy ProfileInfoList.Filter did, a profile is kept when the user is assigned to
// it as an author or a reviewer on its own lineage, or on one of its "what-if" scenarios while scenarios are displayed.
public class ProfileSearchMyProfilesTests
{
    private static readonly Guid MineAsAuthor = Guid.NewGuid();
    private static readonly Guid MineAsReviewer = Guid.NewGuid();
    private static readonly Guid NotMine = Guid.NewGuid();
    private static readonly Guid ScenarioIHold = Guid.NewGuid();

    [Fact]
    public async Task ListsEveryProfile_WhenDisplayMyProfilesIsOff()
    {
        var service = CreateService(ExternalHoldingRolesOn(MineAsAuthor), PublishedProfile(MineAsAuthor), PublishedProfile(NotMine));

        var results = await service.GetProfileSearchResultsAsync(displayMyProfiles: false, cancellationToken: CancellationToken.None);

        results.Select(profile => profile.Id).Should().BeEquivalentTo([MineAsAuthor, NotMine]);
    }

    [Fact]
    public async Task ListsOnlyTheProfilesTheUserIsAnAuthorOrAReviewerOf_WhenDisplayMyProfilesIsOn()
    {
        var service = CreateService(
            ExternalHoldingRolesOn(MineAsAuthor, MineAsReviewer),
            PublishedProfile(MineAsAuthor),
            PublishedProfile(MineAsReviewer),
            PublishedProfile(NotMine));

        var results = await service.GetProfileSearchResultsAsync(displayMyProfiles: true, cancellationToken: CancellationToken.None);

        results.Select(profile => profile.Id).Should().BeEquivalentTo([MineAsAuthor, MineAsReviewer]);
    }

    [Fact]
    public async Task ListsNothing_WhenTheUserHoldsARoleButNoneOfTheProfilesAreTheirs()
    {
        var service = CreateService(ExternalHoldingRolesOn(Guid.NewGuid()), PublishedProfile(NotMine));

        var results = await service.GetProfileSearchResultsAsync(displayMyProfiles: true, cancellationToken: CancellationToken.None);

        results.Should().BeEmpty();
    }

    // The user holds a role on three profiles - published only, draft only and both - and none on a fourth that has both.
    [Theory]
    [InlineData(true, false, "published-only,both")]
    [InlineData(false, true, "draft-only,both")]
    [InlineData(true, true, "published-only,draft-only,both")]
    [InlineData(false, false, "")]
    public async Task FiltersTheUsersProfilesFurtherByThePublishedAndDraftOptions_WhenDisplayMyProfilesIsOn(
        bool displayPublished,
        bool displayDraft,
        string expectedTitles)
    {
        var publishedOnly = Profile(Guid.NewGuid(), "published-only", published: true, draft: false);
        var draftOnly = Profile(Guid.NewGuid(), "draft-only", published: false, draft: true);
        var both = Profile(Guid.NewGuid(), "both", published: true, draft: true);
        var someoneElses = Profile(Guid.NewGuid(), "someone-elses", published: true, draft: true);
        var service = CreateService(ExternalHoldingRolesOn(publishedOnly.Id, draftOnly.Id, both.Id), publishedOnly, draftOnly, both, someoneElses);

        var results = await service.GetProfileSearchResultsAsync(
            displayPublished: displayPublished,
            displayDraft: displayDraft,
            displayMyProfiles: true,
            cancellationToken: CancellationToken.None);

        results.Select(profile => profile.Title).Should().BeEquivalentTo(expectedTitles.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task CountsARoleOnAWhatIfScenario_OnlyWhileScenariosAreDisplayed(bool displayScenarios, bool expectedListed)
    {
        var profile = Profile(NotMine, "Bovine tuberculosis", published: true, scenarios: [Scenario(ScenarioIHold)]);
        var service = CreateService(ExternalHoldingRolesOn(ScenarioIHold), profile);

        var results = await service.GetProfileSearchResultsAsync(displayScenarios: displayScenarios, displayMyProfiles: true, cancellationToken: CancellationToken.None);

        results.Should().HaveCount(expectedListed ? 1 : 0);
    }

    [Fact]
    public async Task KeepsAllTheScenariosOfAProfileTheUserHoldsARoleOn_AsLegacyListedTheProfileNotJustTheirScenario()
    {
        var profile = Profile(MineAsAuthor, "Bovine tuberculosis", published: true, scenarios: [Scenario(Guid.NewGuid()), Scenario(Guid.NewGuid())]);
        var service = CreateService(ExternalHoldingRolesOn(MineAsAuthor), profile);

        var results = await service.GetProfileSearchResultsAsync(displayScenarios: true, displayMyProfiles: true, cancellationToken: CancellationToken.None);

        results.Should().ContainSingle().Which.WhatIfScenarios.Should().HaveCount(2);
    }

    [Fact]
    public async Task AlsoAppliesTheSearchText_WhenDisplayMyProfilesIsOn()
    {
        var service = CreateService(
            ExternalHoldingRolesOn(MineAsAuthor, MineAsReviewer),
            Profile(MineAsAuthor, "Bovine tuberculosis"),
            Profile(MineAsReviewer, "Avian influenza"),
            Profile(NotMine, "Bovine anaplasmosis"));

        var results = await service.GetProfileSearchResultsAsync(searchText: "bovine", displayMyProfiles: true, cancellationToken: CancellationToken.None);

        results.Should().ContainSingle().Which.Id.Should().Be(MineAsAuthor);
    }

    // The filter is offered only to a user who is assigned to a profile, so for anyone else it has no effect rather than hiding everything.
    [Fact]
    public async Task HasNoEffect_ForAUserWhoHoldsNoProfileRole()
    {
        var service = CreateService(ExternalHoldingRolesOn(), PublishedProfile(MineAsAuthor), PublishedProfile(NotMine));

        var results = await service.GetProfileSearchResultsAsync(displayMyProfiles: true, cancellationToken: CancellationToken.None);

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task HasNoEffect_ForAnAnonymousRequest()
    {
        var service = CreateService(new RequestUserContext(), PublishedProfile(MineAsAuthor), PublishedProfile(NotMine));

        var results = await service.GetProfileSearchResultsAsync(displayMyProfiles: true, cancellationToken: CancellationToken.None);

        results.Should().HaveCount(2);
    }

    // Legacy read every assignment of the user, whatever global role they also held, so an editor or a policy profile user who was
    // also made an author or a reviewer has profiles of their own and can filter to them.
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task ListsOnlyTheirProfiles_ForAGlobalRoleUser_WhoIsAlsoAnAuthorOrAReviewer(bool isProfileEditor, bool isPolicyProfileUser)
    {
        var service = CreateService(Internal(isProfileEditor, isPolicyProfileUser, MineAsAuthor), PublishedProfile(MineAsAuthor), PublishedProfile(NotMine));

        var results = await service.GetProfileSearchResultsAsync(displayMyProfiles: true, cancellationToken: CancellationToken.None);

        results.Should().ContainSingle().Which.Id.Should().Be(MineAsAuthor);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task HasNoEffect_ForAGlobalRoleUser_WhoIsAssignedToNoProfile(bool isProfileEditor, bool isPolicyProfileUser)
    {
        var service = CreateService(Internal(isProfileEditor, isPolicyProfileUser), PublishedProfile(MineAsAuthor), PublishedProfile(NotMine));

        var results = await service.GetProfileSearchResultsAsync(displayMyProfiles: true, cancellationToken: CancellationToken.None);

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task HasNoEffect_ForAUserWhoIsNeitherInternalNorExternal()
    {
        var unclassified = ContextOf(new ActingUser
        {
            Id = Guid.NewGuid(),
            IsProfileEditor = false,
            IsPolicyProfileUser = false,
            ProfileRoles = Roles([MineAsAuthor])
        });
        var service = CreateService(unclassified, PublishedProfile(MineAsAuthor), PublishedProfile(NotMine));

        var results = await service.GetProfileSearchResultsAsync(displayMyProfiles: true, cancellationToken: CancellationToken.None);

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task ListsOnlyTheirProfiles_ForAnInternalUserWithNoGlobalRole()
    {
        var service = CreateService(Internal(isProfileEditor: false, isPolicyProfileUser: false, MineAsAuthor), PublishedProfile(MineAsAuthor), PublishedProfile(NotMine));

        var results = await service.GetProfileSearchResultsAsync(displayMyProfiles: true, cancellationToken: CancellationToken.None);

        results.Should().ContainSingle().Which.Id.Should().Be(MineAsAuthor);
    }

    [Fact]
    public void CanFilterToMyProfiles_IsTrue_ForAUserWhoIsAnAuthorOrAReviewerOfAProfile()
    {
        CreateService(ExternalHoldingRolesOn(MineAsReviewer)).CanFilterToMyProfiles.Should().BeTrue();
        CreateService(Internal(isProfileEditor: false, isPolicyProfileUser: false, MineAsAuthor)).CanFilterToMyProfiles.Should().BeTrue();
    }

    [Fact]
    public void CanFilterToMyProfiles_IsFalse_ForAUserWhoHoldsNoProfileRole()
    {
        CreateService(ExternalHoldingRolesOn()).CanFilterToMyProfiles.Should().BeFalse();
        CreateService(Internal(isProfileEditor: false, isPolicyProfileUser: false)).CanFilterToMyProfiles.Should().BeFalse();
        CreateService(new RequestUserContext()).CanFilterToMyProfiles.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void CanFilterToMyProfiles_IsTrue_ForAGlobalRoleUser_WhoIsAlsoAnAuthorOrAReviewer(bool isProfileEditor, bool isPolicyProfileUser) =>
        CreateService(Internal(isProfileEditor, isPolicyProfileUser, MineAsAuthor)).CanFilterToMyProfiles.Should().BeTrue();

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void CanFilterToMyProfiles_IsFalse_ForAGlobalRoleUser_WhoIsAssignedToNoProfile(bool isProfileEditor, bool isPolicyProfileUser) =>
        CreateService(Internal(isProfileEditor, isPolicyProfileUser)).CanFilterToMyProfiles.Should().BeFalse();

    private static ProfileSearchService CreateService(IUserContext userContext, params ProfileSearchResultDto[] profiles)
    {
        var repository = new Mock<IProfileRepository>();
        repository
            .Setup(r => r.GetAllProfilesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileSearchResultDto>)profiles);

        return new ProfileSearchService(repository.Object, userContext);
    }

    private static RequestUserContext ExternalHoldingRolesOn(params Guid[] profileIds) => ContextOf(new ActingUser
    {
        Id = Guid.NewGuid(),
        IsProfileEditor = false,
        IsPolicyProfileUser = false,
        SsoUserIdExt = Guid.NewGuid(),
        ProfileRoles = Roles(profileIds)
    });

    private static RequestUserContext Internal(bool isProfileEditor, bool isPolicyProfileUser, params Guid[] profileIds) => ContextOf(new ActingUser
    {
        Id = Guid.NewGuid(),
        IsProfileEditor = isProfileEditor,
        IsPolicyProfileUser = isPolicyProfileUser,
        SsoUserIdInt = Guid.NewGuid(),
        ProfileRoles = Roles(profileIds)
    });

    private static ProfileRoleAssignment[] Roles(Guid[] profileIds) =>
        [.. profileIds.Select((profileId, index) => new ProfileRoleAssignment(profileId, Guid.NewGuid(), index % 2 == 0 ? "VA author" : "Policy reviewer", index % 2 == 0))];

    private static RequestUserContext ContextOf(ActingUser user)
    {
        var context = new RequestUserContext();
        context.SetActingUser(user);

        return context;
    }

    private static ProfileSearchResultDto PublishedProfile(Guid id) => Profile(id, $"Profile {id}");

    private static ProfileSearchResultDto Profile(
        Guid id,
        string title,
        bool published = true,
        bool draft = false,
        IReadOnlyList<ProfileScenarioDto>? scenarios = null) => new()
        {
            Id = id,
            Title = title,
            Status = published ? "Published" : "Draft",
            CreatedAtUtc = DateTime.UtcNow,
            ModifiedAtUtc = DateTime.UtcNow,
            IsPublic = true,
            AffectedSpecies = [],
            PublishedVersions = published ? [Version()] : [],
            DraftVersions = draft || !published ? [Version()] : [],
            WhatIfScenarios = scenarios ?? []
        };

    // A "what-if" scenario with a published version, so the profile it belongs to is listed when published versions are displayed.
    private static ProfileScenarioDto Scenario(Guid scenarioId) => new()
    {
        ScenarioId = scenarioId,
        PublishedVersions = [Version()],
        DraftVersions = []
    };

    private static ProfileHistoryItemDto Version() => new()
    {
        VersionId = Guid.NewGuid(),
        VersionNumber = 1,
        Title = "Title",
        CreatedAtUtc = DateTime.UtcNow,
        IsScenario = false
    };
}
