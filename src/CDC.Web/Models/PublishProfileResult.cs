using CDC.Common.Contracts;

namespace CDC.Web.Models;

/// <summary>Outcome of <see cref="Infrastructure.IApiClient.PublishProfileAsync"/>.</summary>
public enum PublishProfileOutcome
{
    /// <summary>The draft was published as a new version.</summary>
    Success,

    /// <summary>The draft has validation errors, so nothing was published.</summary>
    InvalidProfile,

    /// <summary>The profile cannot be published in its current state.</summary>
    Conflict,

    /// <summary>The user may not publish the profile.</summary>
    Forbidden,

    /// <summary>The profile no longer exists.</summary>
    NotFound,

    /// <summary>The request was rejected as malformed.</summary>
    ValidationFailed,

    /// <summary>The call failed for another reason.</summary>
    Error
}

/// <summary>Result of attempting to publish a profile's draft version.</summary>
/// <param name="Outcome">What happened.</param>
/// <param name="NewProfileVersionId">The new published version, when <see cref="PublishProfileOutcome.Success"/>.</param>
/// <param name="ErrorMessage">The message to show the user, when the outcome is a failure other than <see cref="PublishProfileOutcome.InvalidProfile"/>.</param>
public sealed record PublishProfileResult(PublishProfileOutcome Outcome, Guid? NewProfileVersionId, string? ErrorMessage)
{
    /// <summary>Gets the validation errors that stopped the draft being published, when <see cref="PublishProfileOutcome.InvalidProfile"/>.</summary>
    public IReadOnlyList<ProfileSectionInvalidReasonDto> InvalidReasons { get; init; } = [];
}
