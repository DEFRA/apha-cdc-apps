namespace CDC.Web.Models;

/// <summary>Outcome of <see cref="Infrastructure.IApiClient.UpdateProfileStatusAsync"/>.</summary>
public enum UpdateProfileStatusOutcome
{
    Success,
    NotFound,
    Error
}

/// <summary>Result of attempting to update a profile's status.</summary>
public sealed record UpdateProfileStatusResult(UpdateProfileStatusOutcome Outcome, string? ErrorMessage);
