using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileManagement.Dtos;

namespace CDC.Api.Features.ProfileManagement.Mapping;

/// <summary>
/// Projects profile management domain entities onto the DTOs returned by the API.
/// </summary>
public static class ProfileManagementMappings
{
    /// <summary>Projects an affected species entity.</summary>
    /// <param name="species">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static AffectedSpeciesDto ToDto(this AffectedSpeciesInfo species) => new()
    {
        SpeciesId = species.SpeciesId,
        Name = species.Name,
        Type = species.Type,
        IsActive = species.IsActive
    };

    /// <summary>Projects a profile entity.</summary>
    /// <param name="profile">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ProfileAttributesDto ToDto(this Profile profile) => new()
    {
        Id = profile.Id,
        Title = profile.Title,
        ScenarioTitle = profile.ScenarioTitle,
        ParentId = profile.ParentId,
        ParentTitle = profile.ParentTitle,
        CurrentDraftProfileVersionId = profile.CurrentDraftProfileVersionId,
        CurrentPublishedProfileVersionId = profile.CurrentPublishedProfileVersionId,
        CurrentPublicVersionId = profile.CurrentPublicVersionId,
        HasPublicScenarios = profile.HasPublicScenarios,
        ProfileStatusId = profile.ProfileStatusId,
        LastUpdated = profile.LastUpdated,
        AffectedSpecies = [.. profile.AffectedSpecies.Select(ToDto)]
    };

    /// <summary>Projects a new-profile-defaults entity.</summary>
    /// <param name="defaults">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static NewProfileDefaultsDto ToDto(this NewProfileDefaults defaults) => new()
    {
        Title = defaults.Title,
        ScenarioTitle = defaults.ScenarioTitle,
        ParentId = defaults.ParentId,
        ParentTitle = defaults.ParentTitle,
        ProfileStatusId = defaults.ProfileStatusId,
        AffectedSpecies = [.. defaults.AffectedSpecies.Select(ToDto)]
    };

    /// <summary>Projects a profile status type entity.</summary>
    /// <param name="statusType">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ProfileStatusTypeDto ToDto(this ProfileStatusType statusType) => new()
    {
        Id = statusType.Id,
        Name = statusType.Name,
        IsValidationComplete = statusType.IsValidationComplete
    };

    /// <summary>Projects a profile creation result entity.</summary>
    /// <param name="result">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static CreateProfileResultDto ToDto(this ProfileCreationResult result) => new()
    {
        NewProfileId = result.NewProfileId,
        NewLastUpdated = result.NewLastUpdated
    };

    /// <summary>Projects a delete-profile-version result entity.</summary>
    /// <param name="result">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static DeleteProfileVersionResultDto ToDto(this DeleteProfileVersionResult result) => new()
    {
        NextLatestProfileVersionId = result.NextLatestProfileVersionId,
        IsProfileDeleted = result.IsProfileDeleted
    };
}
