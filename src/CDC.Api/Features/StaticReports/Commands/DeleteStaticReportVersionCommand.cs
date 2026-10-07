using CDC.Api.Domain.Common;
using MediatR;

namespace CDC.Api.Features.StaticReports.Commands;

/// <summary>
/// Deletes a current static report version - mirrors the legacy <c>StaticReport.CanDelete</c>
/// business rule, which only allows the current version of a report to be removed.
/// </summary>
/// <param name="StaticReportVersionId">The version to delete.</param>
public sealed record DeleteStaticReportVersionCommand(Guid StaticReportVersionId) : IRequest<Result<Unit>>;
