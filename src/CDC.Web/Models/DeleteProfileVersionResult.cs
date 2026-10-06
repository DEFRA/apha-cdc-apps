namespace CDC.Web.Models;

/// <summary>Outcome of <see cref="Infrastructure.IApiClient.DeleteProfileVersionAsync"/>.</summary>
public enum DeleteProfileVersionOutcome
{
    Success,
    NotFound,
    Error
}

/// <summary>Result of attempting to delete a profile version.</summary>
public sealed record DeleteProfileVersionResult(DeleteProfileVersionOutcome Outcome, bool IsProfileDeleted, string? ErrorMessage);
