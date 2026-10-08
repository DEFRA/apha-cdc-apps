namespace CDC.Api.Features.StaticReports.Dtos;

/// <summary>Outcome of <see cref="Interfaces.IStaticReportService.UploadStaticReportAsync"/>.</summary>
public enum UploadStaticReportOutcome
{
    /// <summary>The report or user manual version was uploaded successfully.</summary>
    Success,
    /// <summary>The caller is not permitted to upload the report or user manual version.</summary>
    Forbidden,
    /// <summary>The upload failed validation.</summary>
    ValidationFailed
}

/// <summary>Result of attempting to upload a new static report or user manual version.</summary>
public sealed record UploadStaticReportResult(UploadStaticReportOutcome Outcome, string? ErrorMessage);

/// <summary>Outcome of <see cref="Interfaces.IStaticReportService.DeleteStaticReportVersionAsync"/>.</summary>
public enum DeleteStaticReportVersionOutcome
{
    /// <summary>The report version was deleted successfully.</summary>
    Success,
    /// <summary>The report version could not be found.</summary>
    NotFound,
    /// <summary>The caller is not permitted to delete the report version.</summary>
    Forbidden
}
