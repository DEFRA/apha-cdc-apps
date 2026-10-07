using CDC.Api.Domain.Common;
using CDC.Api.Features.StaticReports.Dtos;
using MediatR;

namespace CDC.Api.Features.StaticReports.Queries;

/// <summary>
/// Retrieves one static report version's PDF content.
/// </summary>
/// <param name="StaticReportVersionId">The version to read.</param>
public sealed record GetStaticReportDataQuery(Guid StaticReportVersionId) : IRequest<Result<StaticReportDataDto>>;
