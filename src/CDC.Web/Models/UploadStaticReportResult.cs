namespace CDC.Web.Models;

/// <summary>Outcome of <see cref="Infrastructure.IApiClient.UploadStaticReportAsync"/>.</summary>
public enum UploadStaticReportOutcome
{
    Success,
    ValidationFailed,
    Forbidden
}

/// <summary>Result of attempting to upload a new static report or user manual version.</summary>
public sealed record UploadStaticReportResult(UploadStaticReportOutcome Outcome, string? ErrorMessage);

/// <summary>Whether the current user may upload static reports or user manuals, from
/// <c>GET /api/static-reports/upload-permission</c>.</summary>
public sealed record StaticReportUploadPermissionDto
{
    /// <summary>Gets a value indicating whether the current user may upload documents.</summary>
    public required bool CanUpload { get; init; }
}
