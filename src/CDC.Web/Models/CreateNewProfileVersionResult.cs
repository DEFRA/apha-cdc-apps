namespace CDC.Web.Models;

/// <summary>Outcome of <see cref="Infrastructure.IApiClient.CreateNewProfileVersionAsync"/>.</summary>
public enum CreateNewProfileVersionOutcome
{
    Success,
    Conflict,
    ValidationFailed,
    Error
}

/// <summary>Result of attempting to create a new profile version.</summary>
public sealed record CreateNewProfileVersionResult(CreateNewProfileVersionOutcome Outcome, Guid? NewProfileVersionId, string? ErrorMessage);
