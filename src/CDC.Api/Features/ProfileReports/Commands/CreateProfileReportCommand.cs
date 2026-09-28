using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileReports.Dtos;
using MediatR;

namespace CDC.Api.Features.ProfileReports.Commands;

/// <summary>
/// Persists a generated report document. Mirrors the legacy <c>CreateProfileReportRequest</c>
/// data contract, plus the rendered document bytes.
/// </summary>
/// <remarks>
/// Legacy rendered <see cref="ReportName"/>'s PDF in-process via TallPDF report classes before
/// persisting it via <c>spiProfileVersionReportData</c>. TallPDF is end-of-life and is being
/// replaced workspace-wide by a Razor views + Playwright pipeline, so this command does not
/// render anything itself: the caller supplies the already-rendered <see cref="ReportData"/>
/// bytes, and this only persists them - exactly what the legacy stored procedure call did.
/// </remarks>
public sealed record CreateProfileReportCommand : IRequest<Result<CreateProfileReportResultDto>>
{
    /// <summary>Gets the profile version the report covers.</summary>
    public required Guid ProfileVersionId { get; init; }

    /// <summary>Gets the report definition being generated (see <c>GET /api/profile-reports/profile-version/{profileVersionId}</c>).</summary>
    public required Guid ProfileReportId { get; init; }

    /// <summary>Gets the internal report name that was used to render <see cref="ReportData"/>.</summary>
    public string ReportName { get; init; } = string.Empty;

    /// <summary>Gets the rendered document bytes to persist.</summary>
    public byte[] ReportData { get; init; } = [];
}
