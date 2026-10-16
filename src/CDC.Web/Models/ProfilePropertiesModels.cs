namespace CDC.Web.Models;

/// <summary>The type of an affected species: the species a profile is about, or another species it affects.</summary>
public static class AffectedSpeciesTypes
{
    public const string Profiled = "Profiled";
    public const string Other = "Other";
}

/// <summary>A species affected by a profile version, as returned by CDC.Api.</summary>
public sealed record AffectedSpeciesDto
{
    public Guid SpeciesId { get; init; }
    public string Name { get; init; } = string.Empty;

    /// <summary><see cref="AffectedSpeciesTypes.Profiled"/> or <see cref="AffectedSpeciesTypes.Other"/>.</summary>
    public string Type { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}

/// <summary>
/// Default values for a new profile or "what-if" scenario, as returned by
/// <c>GET /api/profiles/defaults</c>.
/// </summary>
public sealed record NewProfileDefaultsDto
{
    public string Title { get; init; } = string.Empty;
    public string ScenarioTitle { get; init; } = string.Empty;
    public Guid ParentId { get; init; }
    public string ParentTitle { get; init; } = string.Empty;
    public Guid ProfileStatusId { get; init; }
    public IReadOnlyList<AffectedSpeciesDto> AffectedSpecies { get; init; } = [];

    /// <summary>Describes the cloned version, for example "Anthrax - v1.0 (Published) Valid from 01/01/2020". May contain HTML.</summary>
    public string ClonedVersionDescription { get; init; } = string.Empty;

    /// <summary>Whether the cloned version belongs to a "what-if" scenario.</summary>
    public bool ClonedVersionIsWhatIfScenario { get; init; }
}

/// <summary>One affected species to add to a profile version.</summary>
public sealed record AffectedSpeciesInsertDto
{
    public Guid ProfileVersionId { get; init; }

    /// <summary>The version to copy the species' answers from, or <see cref="Guid.Empty"/> when there is nothing to clone.</summary>
    public Guid CloneProfileVersionId { get; init; }

    public Guid SpeciesId { get; init; }
    public string Type { get; init; } = string.Empty;
}

/// <summary>One affected species to remove from a profile version.</summary>
public sealed record AffectedSpeciesDeleteDto
{
    public Guid ProfileVersionId { get; init; }
    public Guid SpeciesId { get; init; }
}

/// <summary>Request body for <c>POST /api/profiles</c>. Mirrors CDC.Api's <c>CreateProfileCommand</c>.</summary>
public sealed record CreateProfileRequest
{
    public required Guid Id { get; init; }
    public required Guid CurrentDraftProfileVersionId { get; init; }

    /// <summary>The version to clone, or <see cref="Guid.Empty"/> for a brand new profile.</summary>
    public required Guid CloneProfileVersionId { get; init; }

    public string Title { get; init; } = string.Empty;
    public string ScenarioTitle { get; init; } = string.Empty;

    /// <summary>The current-situation profile a new scenario belongs to, or <see cref="Guid.Empty"/> for a profile.</summary>
    public required Guid ParentId { get; init; }

    public string ParentTitle { get; init; } = string.Empty;
    public required Guid ProfileStatusId { get; init; }
    public IReadOnlyList<AffectedSpeciesInsertDto> AffectedSpeciesInsertList { get; init; } = [];
}

/// <summary>
/// Request body for <c>PUT /api/profiles/{profileId}</c>. Mirrors CDC.Api's
/// <c>UpdateProfileAttributesCommand</c>: an empty title or scenario title means "leave unchanged".
/// </summary>
public sealed record UpdateProfileAttributesRequest
{
    public required Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string ScenarioTitle { get; init; } = string.Empty;

    /// <summary>The SQL Server <c>rowversion</c> read alongside the profile, so a concurrent edit can be detected.</summary>
    public required byte[] LastUpdated { get; init; }

    public IReadOnlyList<AffectedSpeciesDeleteDto> AffectedSpeciesDeleteList { get; init; } = [];
    public IReadOnlyList<AffectedSpeciesInsertDto> AffectedSpeciesInsertList { get; init; } = [];
}

/// <summary>Response body of <c>POST /api/profiles</c>.</summary>
public sealed record CreateProfileResultDto
{
    public Guid NewProfileId { get; init; }
}

/// <summary>Outcome of creating or updating a profile.</summary>
public enum SaveProfileOutcome
{
    Success,
    ValidationFailed,
    Conflict,
    Error
}

/// <summary>Result of creating a profile.</summary>
public sealed record CreateProfileResult(SaveProfileOutcome Outcome, Guid? NewProfileId, string? ErrorMessage);

/// <summary>Result of updating a profile's attributes and affected species.</summary>
public sealed record UpdateProfileAttributesResult(SaveProfileOutcome Outcome, string? ErrorMessage);
