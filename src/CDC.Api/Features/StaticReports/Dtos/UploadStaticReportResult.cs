namespace CDC.Api.Features.StaticReports.Dtos;

/// <summary>Outcome of <see cref="Interfaces.IStaticReportService.UploadStaticReportAsync"/>.</summary>
public enum UploadStaticReportOutcome
{
    /// <inheritdoc/>
    Success,
    /// <inheritdoc/>
    Forbidden,
    /// <inheritdoc/>
    ValidationFailed
}

/// <summary>Result of attempting to upload a new static report or user manual version.</summary>
public sealed record UploadStaticReportResult(UploadStaticReportOutcome Outcome, string? ErrorMessage);

/// <summary>Outcome of <see cref="Interfaces.IStaticReportService.DeleteStaticReportVersionAsync"/>.</summary>
public enum DeleteStaticReportVersionOutcome
{
    /// <inheritdoc/>
    Success,
    /// <inheritdoc/>
    NotFound,
    /// <inheritdoc/>
    Forbidden
}
