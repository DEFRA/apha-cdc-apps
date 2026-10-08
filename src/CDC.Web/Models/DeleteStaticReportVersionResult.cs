namespace CDC.Web.Models;

/// <summary>Outcome of <see cref="Infrastructure.IApiClient.DeleteStaticReportVersionAsync"/>.</summary>
public enum DeleteStaticReportVersionOutcome
{
    Success,
    NotFound,
    Forbidden,
    Error
}

/// <summary>Result of attempting to delete a static report or user manual version.</summary>
public sealed record DeleteStaticReportVersionResult(DeleteStaticReportVersionOutcome Outcome, string? ErrorMessage);
