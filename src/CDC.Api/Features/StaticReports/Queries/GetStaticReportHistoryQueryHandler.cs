using CDC.Api.Domain.Common;
using CDC.Api.Features.StaticReports.Dtos;
using CDC.Api.Features.StaticReports.Interfaces;
using CDC.Api.Features.StaticReports.Mapping;
using MediatR;

namespace CDC.Api.Features.StaticReports.Queries;

/// <summary>Handles <see cref="GetStaticReportHistoryQuery"/>.</summary>
/// <param name="repository">Static reports data access.</param>
public sealed class GetStaticReportHistoryQueryHandler(IStaticReportRepository repository)
    : IRequestHandler<GetStaticReportHistoryQuery, Result<IReadOnlyList<StaticReportVersionDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Every version of the report, most recent first.</returns>
    public async Task<Result<IReadOnlyList<StaticReportVersionDto>>> Handle(
        GetStaticReportHistoryQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var versions = await repository.GetHistoryAsync(request.StaticReportId, cancellationToken);

        return Result.Success<IReadOnlyList<StaticReportVersionDto>>([.. versions.Select(version => version.ToDto())]);
    }
}
