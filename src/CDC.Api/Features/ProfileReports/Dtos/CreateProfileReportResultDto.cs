namespace CDC.Api.Features.ProfileReports.Dtos;

/// <summary>Outcome of persisting a generated report. Returned by <c>POST /api/profile-reports</c>.</summary>
public sealed record CreateProfileReportResultDto
{
    /// <summary>Gets the identifier of the persisted report record.</summary>
    public Guid ProfileReportId { get; init; }
}
