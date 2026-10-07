namespace CDC.Web.Models;

/// <summary>
/// Outcome of a call to upload or delete a static report version, distinguishing the failure
/// kinds the page needs to react to differently.
/// </summary>
public enum StaticReportUpdateOutcome
{
    /// <summary>The change was saved.</summary>
    Success,

    /// <summary>The request failed validation (for example, an empty title or empty Pdf data).</summary>
    ValidationFailed,

    /// <summary>The version is not the current version, so it cannot be deleted.</summary>
    Conflict,

    /// <summary>The call to CDC.Api failed unexpectedly.</summary>
    Error
}

/// <summary>Result of a call to upload or delete a static report version.</summary>
public sealed record StaticReportUpdateResult
{
    /// <summary>Gets the outcome classification.</summary>
    public required StaticReportUpdateOutcome Outcome { get; init; }

    /// <summary>Gets a message to show the user when <see cref="Outcome"/> is not <see cref="StaticReportUpdateOutcome.Success"/>.</summary>
    public string? ErrorMessage { get; init; }
}
