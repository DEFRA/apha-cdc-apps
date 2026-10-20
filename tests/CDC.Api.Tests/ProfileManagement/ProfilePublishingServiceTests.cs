using CDC.Api.Application;
using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Domain.Exceptions;
using CDC.Api.Features.ProfileManagement;
using CDC.Api.Features.ProfileManagement.Commands;
using CDC.Api.Features.ProfileManagement.Interfaces;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Common.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ProfileManagement;

/// <summary>
/// Locks in parity with the legacy <c>ManageProfile.aspx.vb</c> <c>lnkPublish_Click</c> handler and the
/// <c>Profile.Validate()</c> / <c>Profile.Publish()</c> business rules it called.
/// </summary>
public sealed class ProfilePublishingServiceTests
{
    private static readonly Guid ParentId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid ParentPublishedVersionId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid ParentPublicVersionId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    private readonly Mock<IProfileManagementRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<IProfileSectionService> sectionService = new(MockBehavior.Strict);

    private ProfilePublishingService CreateService(IUserContext? userContext = null) =>
        new(
            repository.Object,
            sectionService.Object,
            userContext ?? ManageProfileLinkVisibilityTests.TestUserContext.ProfileEditor,
            NullLogger<ProfilePublishingService>.Instance);

    private static Profile ProfileWithDraft() => ProfileManagementTestData.Profile();

    private static Profile ScenarioWithDraft() => ProfileManagementTestData.Profile() with { ParentId = ParentId };

    private static Profile Parent(Guid publishedVersionId = default, Guid publicVersionId = default) =>
        ProfileManagementTestData.Profile() with
        {
            Id = ParentId,
            CurrentDraftProfileVersionId = Guid.Empty,
            CurrentPublishedProfileVersionId = publishedVersionId,
            CurrentPublicVersionId = publicVersionId
        };

    private void SetUpProfile(Profile profile) =>
        repository
            .Setup(repo => repo.GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profile);

    private void SetUpParent(Profile? parent) =>
        repository
            .Setup(repo => repo.GetProfileAttributesAsync(ParentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(parent);

    private void SetUpValidation(params ProfileSectionInvalidReasonDto[] reasons) =>
        sectionService
            .Setup(service => service.ValidateProfileVersionAsync(ProfileManagementTestData.ProfileVersionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reasons);

    private void SetUpPublication(bool isPublic) =>
        repository
            .Setup(repo => repo.CreateNewProfileVersionAsync(
                new CreateNewProfileVersionCommand(ProfileManagementTestData.ProfileVersionId, IsPublished: true, IsPublic: isPublic),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileManagementTestData.NewProfileVersionId);

    private void VerifyNothingWasPublished() =>
        repository.Verify(
            repo => repo.CreateNewProfileVersionAsync(It.IsAny<CreateNewProfileVersionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);

    [Fact]
    public async Task PublishAsync_ReturnsNotFound_WhenTheProfileDoesNotExist()
    {
        repository
            .Setup(repo => repo.GetProfileAttributesAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Profile?)null);

        var result = await CreateService().PublishAsync(ProfileManagementTestData.ProfileId, isPublic: true, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task PublishAsync_RefusesWithTheLegacyMessage_WhenTheProfileHasNoDraftVersion()
    {
        SetUpProfile(ProfileWithDraft() with { CurrentDraftProfileVersionId = Guid.Empty });

        var result = await CreateService().PublishAsync(ProfileManagementTestData.ProfileId, isPublic: false, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
        result.Error.Should().Be("You cannot publish this profile because it does not have a current draft version.");
        VerifyNothingWasPublished();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishAsync_RefusesWithTheLegacyMessage_WhenTheUserIsNotAProfileEditor(bool isPolicyProfileUser)
    {
        SetUpProfile(ProfileWithDraft());
        var user = new ManageProfileLinkVisibilityTests.TestUserContext(isProfileEditor: false, isUserManagementSystem: false, isPolicyProfileUser);

        var result = await CreateService(user).PublishAsync(ProfileManagementTestData.ProfileId, isPublic: true, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
        result.Error.Should().Be("You do not have permission to publish this profile.");
        VerifyNothingWasPublished();
    }

    [Fact]
    public async Task PublishAsync_ListsTheValidationErrorsAndPublishesNothing_WhenTheDraftIsInvalid()
    {
        ProfileSectionInvalidReasonDto[] reasons =
        [
            new() { SectionNumber = 1, Description = "\"Revision dates, Next policy review\" is required" },
            new() { SectionNumber = 3, QuestionNumber = 2, Description = "\"Is it endemic? - Endemic\" is required" }
        ];
        SetUpProfile(ProfileWithDraft());
        SetUpValidation(reasons);

        var result = await CreateService().PublishAsync(ProfileManagementTestData.ProfileId, isPublic: true, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.NewProfileVersionId.Should().BeNull();
        result.Value.InvalidReasons.Should().Equal(reasons);
        VerifyNothingWasPublished();
    }

    [Fact]
    public async Task PublishAsync_ListsTheValidationErrorsBeforeCheckingTheScenarioRules()
    {
        // The legacy page validated first, so a scenario of an unpublished profile with errors showed the errors; the
        // strict repository mock fails the test if the profile's parent is read before the draft has been validated.
        ProfileSectionInvalidReasonDto[] reasons = [new() { SectionNumber = 2, Description = "\"Is it endemic? - Endemic\" is required" }];
        SetUpProfile(ScenarioWithDraft());
        SetUpValidation(reasons);

        var result = await CreateService().PublishAsync(ProfileManagementTestData.ProfileId, isPublic: false, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.InvalidReasons.Should().Equal(reasons);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PublishAsync_PublishesTheValidDraft_AsPublicOnlyWhenAsked(bool isPublic)
    {
        SetUpProfile(ProfileWithDraft());
        SetUpValidation();
        SetUpPublication(isPublic);

        var result = await CreateService().PublishAsync(ProfileManagementTestData.ProfileId, isPublic, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.NewProfileVersionId.Should().Be(ProfileManagementTestData.NewProfileVersionId);
        result.Value.InvalidReasons.Should().BeEmpty();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PublishAsync_RefusesAScenarioWithTheLegacyMessage_WhenItsProfileIsNotPublished(bool isPublic)
    {
        SetUpProfile(ScenarioWithDraft());
        SetUpValidation();
        SetUpParent(Parent());

        var result = await CreateService().PublishAsync(ProfileManagementTestData.ProfileId, isPublic, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
        result.Error.Should().Be("You cannot publish this scenario because the profile to which it belongs is not published.");
        VerifyNothingWasPublished();
    }

    [Fact]
    public async Task PublishAsync_RefusesAScenarioWithTheLegacyMessage_WhenItsProfileNoLongerExists()
    {
        SetUpProfile(ScenarioWithDraft());
        SetUpValidation();
        SetUpParent(null);

        var result = await CreateService().PublishAsync(ProfileManagementTestData.ProfileId, isPublic: false, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
        result.Error.Should().Be("You cannot publish this scenario because the profile to which it belongs is not published.");
    }

    [Fact]
    public async Task PublishAsync_RefusesAPublicScenarioWithTheLegacyMessage_WhenItsProfileHasNoPublicVersion()
    {
        SetUpProfile(ScenarioWithDraft());
        SetUpValidation();
        SetUpParent(Parent(publishedVersionId: ParentPublishedVersionId));

        var result = await CreateService().PublishAsync(ProfileManagementTestData.ProfileId, isPublic: true, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Conflict);
        result.Error.Should().Be("You cannot make this scenario public because there is no public version of this profile.");
        VerifyNothingWasPublished();
    }

    [Fact]
    public async Task PublishAsync_PublishesAScenarioForDefranetOnly_WhenItsProfileIsPublishedButHasNoPublicVersion()
    {
        SetUpProfile(ScenarioWithDraft());
        SetUpValidation();
        SetUpParent(Parent(publishedVersionId: ParentPublishedVersionId));
        SetUpPublication(isPublic: false);

        var result = await CreateService().PublishAsync(ProfileManagementTestData.ProfileId, isPublic: false, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.NewProfileVersionId.Should().Be(ProfileManagementTestData.NewProfileVersionId);
    }

    [Fact]
    public async Task PublishAsync_PublishesAScenarioPublicly_WhenItsProfileHasAPublicVersion()
    {
        SetUpProfile(ScenarioWithDraft());
        SetUpValidation();
        SetUpParent(Parent(publishedVersionId: ParentPublishedVersionId, publicVersionId: ParentPublicVersionId));
        SetUpPublication(isPublic: true);

        var result = await CreateService().PublishAsync(ProfileManagementTestData.ProfileId, isPublic: true, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.NewProfileVersionId.Should().Be(ProfileManagementTestData.NewProfileVersionId);
    }

    [Fact]
    public async Task PublishAsync_PropagatesTheRepositorysConcurrencyException_ForTheHandlerToReport()
    {
        SetUpProfile(ProfileWithDraft());
        SetUpValidation();
        repository
            .Setup(repo => repo.CreateNewProfileVersionAsync(It.IsAny<CreateNewProfileVersionCommand>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("Version 1.2 is not the latest version of this profile. The profile may have been altered by another user."));

        var act = () => CreateService().PublishAsync(ProfileManagementTestData.ProfileId, isPublic: true, CancellationToken.None);

        await act.Should().ThrowAsync<ConcurrencyException>();
    }
}
