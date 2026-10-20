using CDC.Api.Application;
using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileManagement.Commands;
using CDC.Api.Features.ProfileManagement.Interfaces;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Common.Contracts;

namespace CDC.Api.Features.ProfileManagement;

/// <summary>
/// Default <see cref="IProfilePublishingService"/>, replicating the legacy <c>ManageProfile.aspx.vb</c>
/// <c>lnkPublish_Click</c> handler and the <c>Profile.Publish</c> business rules it relied on. The legacy checks of
/// <c>NewProfileVersionCommand</c> (the draft is the latest version, it has an active profiled species, it is not already
/// published) stay in <see cref="IProfileManagementRepository.CreateNewProfileVersionAsync"/>, which must evaluate them in
/// the transaction that creates the version.
/// </summary>
/// <param name="repository">Profile management data access.</param>
/// <param name="profileSectionService">Validates every section of the draft version.</param>
/// <param name="userContext">The current user's profile-authoring role flags.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ProfilePublishingService(
    IProfileManagementRepository repository,
    IProfileSectionService profileSectionService,
    IUserContext userContext,
    ILogger<ProfilePublishingService> logger)
    : IProfilePublishingService
{
    /// <summary>The legacy <c>Profile.Publish</c> message when the profile has no draft to publish.</summary>
    public const string NoDraftVersionMessage = "You cannot publish this profile because it does not have a current draft version.";

    /// <summary>The legacy <c>Profile.Publish</c> message when the user is not a profile editor.</summary>
    public const string NotPermittedMessage = "You do not have permission to publish this profile.";

    /// <summary>The legacy <c>Profile.Publish</c> message when a scenario's profile has no published version.</summary>
    public const string ScenarioNotPublishedMessage = "You cannot publish this scenario because the profile to which it belongs is not published.";

    /// <summary>The legacy <c>Profile.Publish</c> message when a public scenario's profile has no public version.</summary>
    public const string ScenarioNotPublicMessage = "You cannot make this scenario public because there is no public version of this profile.";

    /// <inheritdoc />
    public async Task<Result<PublishProfileResultDto>> PublishAsync(Guid profileId, bool isPublic, CancellationToken cancellationToken)
    {
        var profile = await repository.GetProfileAttributesAsync(profileId, cancellationToken);

        if (profile is null)
        {
            logger.ProfileNotFound(profileId);

            return Result.NotFound<PublishProfileResultDto>($"Profile '{profileId}' was not found.");
        }

        var draftVersionId = profile.CurrentDraftProfileVersionId;

        if (draftVersionId == Guid.Empty)
        {
            logger.PublishRefused(profileId, NoDraftVersionMessage);

            return Result.Conflict<PublishProfileResultDto>(NoDraftVersionMessage);
        }

        if (!userContext.IsProfileEditor)
        {
            logger.PublishRefused(profileId, NotPermittedMessage);

            return Result.Forbidden<PublishProfileResultDto>(NotPermittedMessage);
        }

        // The legacy page validated first and published nothing when there was any error to list.
        var invalidReasons = await profileSectionService.ValidateProfileVersionAsync(draftVersionId, cancellationToken);

        if (invalidReasons.Count > 0)
        {
            logger.PublishBlockedByValidation(profileId, draftVersionId, invalidReasons.Count);

            return Result.Success(new PublishProfileResultDto { InvalidReasons = invalidReasons });
        }

        var scenarioRefusal = await CheckScenarioRulesAsync(profile, isPublic, cancellationToken);

        if (scenarioRefusal is not null)
        {
            logger.PublishRefused(profileId, scenarioRefusal);

            return Result.Conflict<PublishProfileResultDto>(scenarioRefusal);
        }

        var newProfileVersionId = await repository.CreateNewProfileVersionAsync(
            new CreateNewProfileVersionCommand(draftVersionId, IsPublished: true, IsPublic: isPublic),
            cancellationToken);
        logger.PublishedProfile(profileId, newProfileVersionId, draftVersionId, isPublic);

        return Result.Success(new PublishProfileResultDto { NewProfileVersionId = newProfileVersionId });
    }

    /// <summary>A "what-if" scenario can only be published when its profile is, and made public when its profile has a public version.</summary>
    /// <returns>The legacy message of the rule the publication breaks, or <see langword="null"/> when the profile is not a scenario or its profile allows the publication.</returns>
    private async Task<string?> CheckScenarioRulesAsync(Domain.Entities.Profile profile, bool isPublic, CancellationToken cancellationToken)
    {
        if (profile.ParentId == Guid.Empty)
        {
            return null;
        }

        var parent = await repository.GetProfileAttributesAsync(profile.ParentId, cancellationToken);

        if (parent is null || parent.CurrentPublishedProfileVersionId == Guid.Empty)
        {
            return ScenarioNotPublishedMessage;
        }

        return isPublic && parent.CurrentPublicVersionId == Guid.Empty ? ScenarioNotPublicMessage : null;
    }
}
