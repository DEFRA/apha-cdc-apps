using CDC.Api.Domain.Common;
using CDC.Api.Features.StaticReports.Dtos;
using MediatR;

namespace CDC.Api.Features.StaticReports.Queries;

/// <summary>
/// Retrieves the current version of every static report, optionally filtered to user manuals.
/// </summary>
/// <param name="IsUserManual">Whether to retrieve user manuals rather than general reports.</param>
public sealed record GetCurrentStaticReportsQuery(bool IsUserManual) : IRequest<Result<IReadOnlyList<StaticReportVersionDto>>>;
