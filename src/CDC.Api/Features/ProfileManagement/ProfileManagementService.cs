using CDC.Api.Application;
using CDC.Api.Features.ProfileManagement.Commands;
using CDC.Api.Features.ProfileManagement.Dtos;
using CDC.Api.Features.ProfileManagement.Interfaces;
using CDC.Api.Features.ProfileManagement.Mapping;
using CDC.Common.Contracts;

namespace CDC.Api.Features.ProfileManagement;

/// <summary>
/// Default <see cref="IProfileManagementService"/>: reads and writes through
/// <see cref="IProfileManagementRepository"/> and maps domain entities onto the DTOs the API
/// returns.
/// </summary>
/// <param name="repository">Profile management data access.</param>
/// <param name="userContext">The current user's profile-authoring role flags.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ProfileManagementService(
    IProfileManagementRepository repository,
    IUserContext userContext,
    ILogger<ProfileManagementService> logger)
    : IProfileManagementService
{
    /// <inheritdoc />
    public async Task<CreateProfileResultDto> CreateProfileAsync(CreateProfileCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var result = await repository.CreateProfileAsync(command, cancellationToken);
        logger.CreatedProfile(result.NewProfileId);

        return result.ToDto();
    }

    /// <inheritdoc />
    public async Task<UpdateProfileAttributesResultDto> UpdateProfileAttributesAsync(
        UpdateProfileAttributesCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var newLastUpdated = await repository.UpdateProfileAttributesAsync(command, cancellationToken);
        logger.UpdatedProfileAttributes(command.Id);

        return new UpdateProfileAttributesResultDto { NewLastUpdated = newLastUpdated };
    }

    /// <inheritdoc />
    public async Task<DeleteProfileVersionResultDto?> DeleteProfileVersionAsync(
        Guid profileVersionId,
        CancellationToken cancellationToken)
    {
        var result = await repository.DeleteProfileVersionAsync(profileVersionId, cancellationToken);

        if (result is null)
        {
            logger.ProfileVersionNotFoundForDeletion(profileVersionId);
            return null;
        }

        logger.DeletedProfileVersion(profileVersionId);

        return result.ToDto();
    }

    /// <inheritdoc />
    public async Task<NewProfileVersionResultDto> CreateNewProfileVersionAsync(
        CreateNewProfileVersionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var newProfileVersionId = await repository.CreateNewProfileVersionAsync(command, cancellationToken);
        logger.CreatedNewProfileVersion(newProfileVersionId, command.ProfileVersionId);

        return new NewProfileVersionResultDto { NewProfileVersionId = newProfileVersionId };
    }

    /// <inheritdoc />
    public async Task<ProfileAttributesDto?> GetProfileAttributesAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var profile = await repository.GetProfileAttributesAsync(profileId, cancellationToken);

        if (profile is null)
        {
            logger.ProfileNotFound(profileId);
            return null;
        }

        logger.RetrievedProfileAttributes(profileId);

        return profile.ToDto();
    }

    /// <inheritdoc />
    public async Task<NewProfileDefaultsDto?> GetNewProfileDefaultsAsync(
        Guid cloneProfileVersionId,
        bool isWhatIfScenario,
        CancellationToken cancellationToken)
    {
        var defaults = await repository.GetNewProfileDefaultsAsync(cloneProfileVersionId, isWhatIfScenario, cancellationToken);

        if (defaults is null)
        {
            return null;
        }

        logger.RetrievedNewProfileDefaults(cloneProfileVersionId);

        return defaults.ToDto();
    }

    /// <inheritdoc />
    public async Task<AffectedSpeciesDto?> GetAffectedSpeciesAsync(Guid speciesId, CancellationToken cancellationToken)
    {
        var species = await repository.GetAffectedSpeciesAsync(speciesId, cancellationToken);

        return species?.ToDto();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileStatusTypeDto>> GetProfileStatusTypesAsync(CancellationToken cancellationToken)
    {
        var statusTypes = await repository.GetProfileStatusTypesAsync(cancellationToken);
        logger.RetrievedProfileStatusTypes(statusTypes.Count);

        return [.. statusTypes.Select(status => status.ToDto())];
    }

    /// <inheritdoc />
    public async Task SetProfileVersionPublicAccessAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        await repository.SetProfileVersionPublicAccessAsync(profileVersionId, cancellationToken);
        logger.TogglePublicAccess(profileVersionId);
    }

    /// <inheritdoc />
    public async Task UpdateProfileStatusAsync(Guid profileId, Guid profileStatusId, CancellationToken cancellationToken)
    {
        await repository.UpdateProfileStatusAsync(profileId, profileStatusId, cancellationToken);
        logger.UpdatedProfileStatus(profileId, profileStatusId);
    }

    /// <inheritdoc />
    public async Task<GetManageProfileResponse?> GetManageProfileAsync(Guid profileId, CancellationToken cancellationToken)
    {
        var profile = await repository.GetProfileAttributesAsync(profileId, cancellationToken);

        if (profile is null)
        {
            logger.ProfileNotFound(profileId);
            return null;
        }

        var publicVersionLabel = await FormatVersionLabelAsync(profile.CurrentPublicVersionId, cancellationToken);
        var publishedVersionLabel = await FormatVersionLabelAsync(profile.CurrentPublishedProfileVersionId, cancellationToken);
        var draftVersionLabel = await FormatVersionLabelAsync(profile.CurrentDraftProfileVersionId, cancellationToken);
        var statusName = await ResolveProfileStatusNameAsync(profile.ProfileStatusId, cancellationToken);
        var linkVisibility = await BuildLinkVisibilityAsync(profile, cancellationToken);

        logger.RetrievedProfileAttributes(profileId);

        return new GetManageProfileResponse
        {
            ProfileId = profile.Id,
            ProfileTitle = profile.Title,
            ScenarioTitle = profile.ParentId == Guid.Empty ? "Current situation" : profile.ScenarioTitle,
            LatestPublishedVersionPublic = publicVersionLabel,
            LatestPublishedVersionDefraNetOnly = publishedVersionLabel,
            LatestDraftVersion = draftVersionLabel,
            ProfileStatus = statusName,
            ProfileStatusId = profile.ProfileStatusId,
            CurrentProfileVersionId = ResolveCurrentProfileVersionId(profile),
            LinkVisibility = linkVisibility
        };
    }

    /// <summary>
    /// Computes the "Manage profile" action link visibility, matching
    /// <c>ManageProfile.aspx.vb</c>'s <c>RefreshDisplay</c> and the underlying
    /// <c>Profile.vb</c>/<c>ProfileContributorList.vb</c> rules verbatim, including their exact
    /// short-circuit order.
    /// </summary>
    private async Task<ManageProfileLinkVisibilityDto> BuildLinkVisibilityAsync(
        Domain.Entities.Profile profile,
        CancellationToken cancellationToken)
    {
        var isWhatIfScenario = profile.ParentId != Guid.Empty;
        var hasCurrentDraftVersion = profile.CurrentDraftProfileVersionId != Guid.Empty;
        var hasCurrentPublishedVersion = profile.CurrentPublishedProfileVersionId != Guid.Empty;

        // Only fetched for a what-if scenario: ParentProfile.CurrentPublishedVersion/HasPublicVersion.
        var parentProfile = isWhatIfScenario
            ? await repository.GetProfileAttributesAsync(profile.ParentId, cancellationToken)
            : null;
        var parentHasPublishedVersion = parentProfile is not null && parentProfile.CurrentPublishedProfileVersionId != Guid.Empty;
        var parentHasPublicVersion = parentProfile is not null && parentProfile.CurrentPublicVersionId != Guid.Empty;

        var parentPublishedVersionIsPublic = false;
        if (parentHasPublishedVersion)
        {
            var parentPublishedVersion = await repository.GetProfileVersionSummaryAsync(
                parentProfile!.CurrentPublishedProfileVersionId, cancellationToken);
            parentPublishedVersionIsPublic = parentPublishedVersion?.IsPublic ?? false;
        }

        var currentPublishedVersionIsPublic = false;
        if (hasCurrentPublishedVersion)
        {
            var currentPublishedVersion = await repository.GetProfileVersionSummaryAsync(
                profile.CurrentPublishedProfileVersionId, cancellationToken);
            currentPublishedVersionIsPublic = currentPublishedVersion?.IsPublic ?? false;
        }

        // ProfileContributorList.CanGetContributorList() / Profile.CanCreateProfile() /
        // Profile.CanEditProfile(): all identity.IsProfileEditor AndAlso Not IsUserManagementSystem.
        var isProfileEditorNotUserManagement = userContext.IsProfileEditor && !userContext.IsUserManagementSystem;

        // Profile.CanPublish(): false with no draft; false for a what-if scenario whose parent
        // has no published version; otherwise identity.IsProfileEditor.
        bool canPublish;
        if (!hasCurrentDraftVersion)
        {
            canPublish = false;
        }
        else if (isWhatIfScenario && !parentHasPublishedVersion)
        {
            canPublish = false;
        }
        else
        {
            canPublish = userContext.IsProfileEditor;
        }

        // Profile.CanPublishPublic(): for a what-if scenario whose parent has a published
        // version, only when that published version is itself public; otherwise same as CanPublish().
        var canPublishPublic = isWhatIfScenario && parentHasPublishedVersion
            ? canPublish && parentPublishedVersionIsPublic
            : canPublish;

        // SetProfileVersionPublicAccessCommand.CanChangePublicAccess(profile.Id).
        bool canChangePublicAccess;
        if (!hasCurrentPublishedVersion)
        {
            canChangePublicAccess = false;
        }
        else if (currentPublishedVersionIsPublic)
        {
            canChangePublicAccess = false;
        }
        else if (isWhatIfScenario && !parentHasPublicVersion)
        {
            canChangePublicAccess = false;
        }
        else
        {
            canChangePublicAccess = isProfileEditorNotUserManagement;
        }

        return new ManageProfileLinkVisibilityDto
        {
            CanEditProperties = isProfileEditorNotUserManagement && (isWhatIfScenario || hasCurrentDraftVersion),
            CanMaintainContributorsAndReviewers = isProfileEditorNotUserManagement,
            CanViewContributionsReport = (userContext.IsProfileEditor || userContext.IsPolicyProfileUser) && !userContext.IsUserManagementSystem,
            CanCreateNewDraftVersion = userContext.IsProfileEditor,
            CanDeleteCurrentVersion = hasCurrentDraftVersion && userContext.IsProfileEditor,
            CanCloneNewProfile = isProfileEditorNotUserManagement && !isWhatIfScenario,
            CanCloneNewScenario = isProfileEditorNotUserManagement,
            CanPublishPublic = canPublishPublic,
            CanPublishDefranetOnly = canPublish,
            CanAllowPublicAccess = canChangePublicAccess && profile.CurrentPublishedProfileVersionId != profile.CurrentPublicVersionId
        };
    }

    /// <summary>Draft take priority, then published, then public - matching the legacy "most
    /// current" version a user expects to browse/edit.</summary>
    private static Guid ResolveCurrentProfileVersionId(Domain.Entities.Profile profile)
    {
        if (profile.CurrentDraftProfileVersionId != Guid.Empty)
        {
            return profile.CurrentDraftProfileVersionId;
        }

        if (profile.CurrentPublishedProfileVersionId != Guid.Empty)
        {
            return profile.CurrentPublishedProfileVersionId;
        }

        return profile.CurrentPublicVersionId;
    }

    private async Task<string> FormatVersionLabelAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        var summary = await repository.GetProfileVersionSummaryAsync(profileVersionId, cancellationToken);

        return summary is null ? "- none -" : $"{summary.VersionMajor}.{summary.VersionMinor}";
    }

    private async Task<string> ResolveProfileStatusNameAsync(Guid profileStatusId, CancellationToken cancellationToken)
    {
        var statusTypes = await repository.GetProfileStatusTypesAsync(cancellationToken);

        return statusTypes.FirstOrDefault(status => status.Id == profileStatusId)?.Name ?? string.Empty;
    }
}
