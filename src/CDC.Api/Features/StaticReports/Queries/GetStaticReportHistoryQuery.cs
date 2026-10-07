using CDC.Api.Domain.Common;
using CDC.Api.Features.StaticReports.Dtos;
using MediatR;

namespace CDC.Api.Features.StaticReports.Queries;

/// <summary>
/// Retrieves every version of one static report, most recent first.
/// </summary>
/// <param name="StaticReportId">The report whose history is being read.</param>
public sealed record GetStaticReportHistoryQuery(Guid StaticReportId) : IRequest<Result<IReadOnlyList<StaticReportVersionDto>>>;
