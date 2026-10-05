using CDC.Api.Application;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileManagement;
using CDC.Api.Features.ProfileManagement.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ProfileManagement;

/// <summary>
/// Locks in parity with the legacy <c>ManageProfile.aspx.vb</c> <c>RefreshDisplay</c> link
/// visibility rules (see <c>Profile.vb</c> / <c>ProfileContributorList.vb</c> /
/// <c>SetProfileVersionPublicAccessCommand.vb</c> / <c>GetContributionsReportCommand.vb</c>),
/// against representative profile states (draft present/absent, what-if true/false, parent
/// published/unpublished, public/non-public).
/// </summary>
public sealed class ManageProfileLinkVisibilityTests
{
    private static readonly Guid ParentId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid PublishedVersionId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid PublicVersionId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid ParentPublishedVersionId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    private readonly Mock<IProfileManagementRepository> repository = new(MockBehavior.Strict);

    private ProfileManagementService CreateService(TestUserContext? userContext = null) =>
        new(repository.Object, userContext ?? TestUserContext.ProfileEditor, NullLogger<ProfileManagementService>.Instance);

    private void SetUpProfile(Profile profile)
    {
        repository
            .Setup(repo => repo.GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProfileVersionSummary?)null);
        repository
            .Setup(repo => repo.GetProfileStatusTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<ProfileStatusType>)[]);
    }

    private void SetUpParentProfile(Profile? parentProfile) =>
        repository
            .Setup(repo => repo.GetProfileAttributesAsync(ParentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parentProfile);

    private void SetUpVersionPublicStatus(Guid profileVersionId, bool isPublic) =>
        repository
            .Setup(repo => repo.GetProfileVersionSummaryAsync(profileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProfileVersionSummary { VersionMajor = 1, VersionMinor = 0, IsPublic = isPublic });

    private static Profile CurrentSituationProfile(Guid draftVersionId = default, Guid publishedVersionId = default, Guid publicVersionId = default) =>
        ProfileManagementTestData.Profile() with
        {
            ParentId = Guid.Empty,
            CurrentDraftProfileVersionId = draftVersionId,
            CurrentPublishedProfileVersionId = publishedVersionId,
            CurrentPublicVersionId = publicVersionId
        };

    private static Profile WhatIfScenarioProfile(Guid draftVersionId = default) =>
        ProfileManagementTestData.Profile() with
        {
            ParentId = ParentId,
            CurrentDraftProfileVersionId = draftVersionId,
            CurrentPublishedProfileVersionId = Guid.Empty,
            CurrentPublicVersionId = Guid.Empty
        };

    private async Task<CDC.Common.Contracts.ManageProfileLinkVisibilityDto> GetVisibilityAsync(TestUserContext? userContext = null)
    {
        var result = await CreateService(userContext).GetManageProfileAsync(ProfileManagementTestData.ProfileId, CancellationToken.None);

        return result!.LinkVisibility;
    }

    // 1. Edit properties: CanEditProfile() AndAlso (IsWhatIfScenario OrElse CurrentDraftVersion IsNot Nothing).
    [Fact]
    public async Task CanEditProperties_IsTrue_ForACurrentSituationProfileWithADraft()
    {
        SetUpProfile(CurrentSituationProfile(draftVersionId: ProfileManagementTestData.ProfileVersionId));

        var visibility = await GetVisibilityAsync();

        visibility.CanEditProperties.Should().BeTrue();
    }

    [Fact]
    public async Task CanEditProperties_IsFalse_ForACurrentSituationProfileWithNoDraft()
    {
        SetUpProfile(CurrentSituationProfile());

        var visibility = await GetVisibilityAsync();

        visibility.CanEditProperties.Should().BeFalse();
    }

    [Fact]
    public async Task CanEditProperties_IsTrue_ForAWhatIfScenarioEvenWithNoDraft()
    {
        var profile = WhatIfScenarioProfile();
        SetUpProfile(profile);
        SetUpParentProfile(null);

        var visibility = await GetVisibilityAsync();

        visibility.CanEditProperties.Should().BeTrue();
    }

    [Fact]
    public async Task CanEditProperties_IsFalse_WhenUserIsUserManagementSystem()
    {
        SetUpProfile(CurrentSituationProfile(draftVersionId: ProfileManagementTestData.ProfileVersionId));

        var visibility = await GetVisibilityAsync(TestUserContext.UserManagementSystem);

        visibility.CanEditProperties.Should().BeFalse();
    }

    // 2. Maintain contributors and reviewers: ProfileContributorList.CanGetContributorList().
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, false, false)]
    [InlineData(true, true, false)]
    public async Task CanMaintainContributorsAndReviewers_MatchesIsProfileEditorAndNotUserManagementSystem(
        bool isProfileEditor, bool isUserManagementSystem, bool expected)
    {
        SetUpProfile(CurrentSituationProfile());

        var visibility = await GetVisibilityAsync(new TestUserContext(isProfileEditor, isUserManagementSystem, false));

        visibility.CanMaintainContributorsAndReviewers.Should().Be(expected);
    }

    // 3. View contributions report: GetContributionsReportCommand.CanGetReport().
    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(true, true, false, false)]
    [InlineData(false, false, false, false)]
    public async Task CanViewContributionsReport_MatchesEditorOrPolicyUserAndNotUserManagementSystem(
        bool isProfileEditor, bool isUserManagementSystem, bool isPolicyProfileUser, bool expected)
    {
        SetUpProfile(CurrentSituationProfile());

        var visibility = await GetVisibilityAsync(new TestUserContext(isProfileEditor, isUserManagementSystem, isPolicyProfileUser));

        visibility.CanViewContributionsReport.Should().Be(expected);
    }

    // 4. Create new draft version: profile.CanCreateNewDraft() -> identity.IsProfileEditor only.
    [Fact]
    public async Task CanCreateNewDraftVersion_IsTrue_WhenUserIsProfileEditor_EvenIfAlsoUserManagementSystem()
    {
        SetUpProfile(CurrentSituationProfile());

        var visibility = await GetVisibilityAsync(new TestUserContext(true, true, false));

        visibility.CanCreateNewDraftVersion.Should().BeTrue();
    }

    [Fact]
    public async Task CanCreateNewDraftVersion_IsFalse_WhenUserIsNotProfileEditor()
    {
        SetUpProfile(CurrentSituationProfile());

        var visibility = await GetVisibilityAsync(TestUserContext.ReadOnly);

        visibility.CanCreateNewDraftVersion.Should().BeFalse();
    }

    // 5. Delete current version: CurrentDraftProfileVersionId <> Guid.Empty AND identity.IsProfileEditor.
    [Fact]
    public async Task CanDeleteCurrentVersion_IsTrue_WhenADraftExistsAndUserIsProfileEditor()
    {
        SetUpProfile(CurrentSituationProfile(draftVersionId: ProfileManagementTestData.ProfileVersionId));

        var visibility = await GetVisibilityAsync();

        visibility.CanDeleteCurrentVersion.Should().BeTrue();
    }

    [Fact]
    public async Task CanDeleteCurrentVersion_IsFalse_WhenNoDraftExists()
    {
        SetUpProfile(CurrentSituationProfile());

        var visibility = await GetVisibilityAsync();

        visibility.CanDeleteCurrentVersion.Should().BeFalse();
    }

    // 6/7. Clone new profile / Clone new scenario: Profile.CanCreateProfile(), profile excludes what-if scenarios.
    [Fact]
    public async Task CanCloneNewProfile_IsFalse_ForAWhatIfScenario_ButCanCloneNewScenario_IsTrue()
    {
        var profile = WhatIfScenarioProfile();
        SetUpProfile(profile);
        SetUpParentProfile(null);

        var visibility = await GetVisibilityAsync();

        visibility.CanCloneNewProfile.Should().BeFalse();
        visibility.CanCloneNewScenario.Should().BeTrue();
    }

    [Fact]
    public async Task CanCloneNewProfileAndScenario_AreBothTrue_ForACurrentSituationProfile()
    {
        SetUpProfile(CurrentSituationProfile());

        var visibility = await GetVisibilityAsync();

        visibility.CanCloneNewProfile.Should().BeTrue();
        visibility.CanCloneNewScenario.Should().BeTrue();
    }

    [Fact]
    public async Task CanCloneNewProfileAndScenario_AreBothFalse_WhenUserIsUserManagementSystem()
    {
        SetUpProfile(CurrentSituationProfile());

        var visibility = await GetVisibilityAsync(TestUserContext.UserManagementSystem);

        visibility.CanCloneNewProfile.Should().BeFalse();
        visibility.CanCloneNewScenario.Should().BeFalse();
    }

    // 8/9. Publish (public) / Publish (Defranet only): profile.CanPublishPublic() / profile.CanPublish().
    [Fact]
    public async Task Publish_AreBothFalse_WhenThereIsNoDraft()
    {
        SetUpProfile(CurrentSituationProfile());

        var visibility = await GetVisibilityAsync();

        visibility.CanPublishDefranetOnly.Should().BeFalse();
        visibility.CanPublishPublic.Should().BeFalse();
    }

    [Fact]
    public async Task Publish_AreBothTrue_ForACurrentSituationProfileWithADraftAndAnEditor()
    {
        SetUpProfile(CurrentSituationProfile(draftVersionId: ProfileManagementTestData.ProfileVersionId));

        var visibility = await GetVisibilityAsync();

        visibility.CanPublishDefranetOnly.Should().BeTrue();
        visibility.CanPublishPublic.Should().BeTrue();
    }

    [Fact]
    public async Task Publish_AreBothFalse_ForAWhatIfScenario_WhenTheParentHasNoPublishedVersion()
    {
        var profile = WhatIfScenarioProfile(draftVersionId: ProfileManagementTestData.ProfileVersionId);
        SetUpProfile(profile);
        SetUpParentProfile(CurrentSituationProfile()); // ParentId empty => not itself a scenario; no published version.

        var visibility = await GetVisibilityAsync();

        visibility.CanPublishDefranetOnly.Should().BeFalse();
        visibility.CanPublishPublic.Should().BeFalse();
    }

    [Fact]
    public async Task PublishPublic_IsTrue_ForAWhatIfScenario_WhenTheParentsPublishedVersionIsPublic()
    {
        var profile = WhatIfScenarioProfile(draftVersionId: ProfileManagementTestData.ProfileVersionId);
        SetUpProfile(profile);
        SetUpParentProfile(CurrentSituationProfile(publishedVersionId: ParentPublishedVersionId));
        SetUpVersionPublicStatus(ParentPublishedVersionId, isPublic: true);

        var visibility = await GetVisibilityAsync();

        visibility.CanPublishDefranetOnly.Should().BeTrue();
        visibility.CanPublishPublic.Should().BeTrue();
    }

    [Fact]
    public async Task PublishPublic_IsFalse_ForAWhatIfScenario_WhenTheParentsPublishedVersionIsNotPublic()
    {
        var profile = WhatIfScenarioProfile(draftVersionId: ProfileManagementTestData.ProfileVersionId);
        SetUpProfile(profile);
        SetUpParentProfile(CurrentSituationProfile(publishedVersionId: ParentPublishedVersionId));
        SetUpVersionPublicStatus(ParentPublishedVersionId, isPublic: false);

        var visibility = await GetVisibilityAsync();

        // CanPublish is still true (the parent has a published version), only the "public" flavour is gated.
        visibility.CanPublishDefranetOnly.Should().BeTrue();
        visibility.CanPublishPublic.Should().BeFalse();
    }

    // 10. Allow public access: visible unless NOT CanChangePublicAccess() OR CurrentPublishedVersion == CurrentPublicVersion.
    [Fact]
    public async Task CanAllowPublicAccess_IsTrue_WhenPublishedVersionIsNotPublicAndDiffersFromThePublicVersion()
    {
        SetUpProfile(CurrentSituationProfile(publishedVersionId: PublishedVersionId, publicVersionId: PublicVersionId));
        SetUpVersionPublicStatus(PublishedVersionId, isPublic: false);

        var visibility = await GetVisibilityAsync();

        visibility.CanAllowPublicAccess.Should().BeTrue();
    }

    [Fact]
    public async Task CanAllowPublicAccess_IsFalse_WhenThereIsNoCurrentPublishedVersion()
    {
        SetUpProfile(CurrentSituationProfile());

        var visibility = await GetVisibilityAsync();

        visibility.CanAllowPublicAccess.Should().BeFalse();
    }

    [Fact]
    public async Task CanAllowPublicAccess_IsFalse_WhenThePublishedVersionIsAlreadyPublic()
    {
        SetUpProfile(CurrentSituationProfile(publishedVersionId: PublishedVersionId, publicVersionId: PublicVersionId));
        SetUpVersionPublicStatus(PublishedVersionId, isPublic: true);

        var visibility = await GetVisibilityAsync();

        visibility.CanAllowPublicAccess.Should().BeFalse();
    }

    [Fact]
    public async Task CanAllowPublicAccess_IsFalse_WhenThePublishedAndPublicVersionsAreTheSame()
    {
        SetUpProfile(CurrentSituationProfile(publishedVersionId: PublishedVersionId, publicVersionId: PublishedVersionId));
        SetUpVersionPublicStatus(PublishedVersionId, isPublic: false);

        var visibility = await GetVisibilityAsync();

        visibility.CanAllowPublicAccess.Should().BeFalse();
    }

    [Fact]
    public async Task CanAllowPublicAccess_IsFalse_ForAWhatIfScenario_WhenTheParentHasNoPublicVersion()
    {
        var profile = WhatIfScenarioProfile() with
        {
            CurrentPublishedProfileVersionId = PublishedVersionId,
            CurrentPublicVersionId = PublicVersionId
        };
        SetUpProfile(profile);
        SetUpParentProfile(CurrentSituationProfile()); // no CurrentPublicVersionId.
        SetUpVersionPublicStatus(PublishedVersionId, isPublic: false);

        var visibility = await GetVisibilityAsync();

        visibility.CanAllowPublicAccess.Should().BeFalse();
    }

    /// <summary>Configurable <see cref="IUserContext"/> test double.</summary>
    internal sealed class TestUserContext(bool isProfileEditor, bool isUserManagementSystem, bool isPolicyProfileUser) : IUserContext
    {
        public static TestUserContext ProfileEditor => new(true, false, false);

        public static TestUserContext ReadOnly => new(false, false, false);

        public static TestUserContext UserManagementSystem => new(true, true, false);

        public bool IsProfileEditor { get; } = isProfileEditor;

        public bool IsUserManagementSystem { get; } = isUserManagementSystem;

        public bool IsPolicyProfileUser { get; } = isPolicyProfileUser;
    }
}
