using CDC.Api.Domain.Common;
using CDC.Api.Features.StaticReports.Dtos;
using CDC.Api.Features.StaticReports.Interfaces;
using CDC.Api.Features.StaticReports.Mapping;
using MediatR;

namespace CDC.Api.Features.StaticReports.Queries;

/// <summary>Handles <see cref="GetCurrentStaticReportsQuery"/>.</summary>
/// <param name="repository">Static reports data access.</param>
public sealed class GetCurrentStaticReportsQueryHandler(IStaticReportRepository repository)
    : IRequestHandler<GetCurrentStaticReportsQuery, Result<IReadOnlyList<StaticReportVersionDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The current version of every matching report.</returns>
    public async Task<Result<IReadOnlyList<StaticReportVersionDto>>> Handle(
        GetCurrentStaticReportsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reports = await repository.GetCurrentAsync(request.IsUserManual, cancellationToken);

        return Result.Success<IReadOnlyList<StaticReportVersionDto>>([.. reports.Select(report => report.ToDto())]);
    }
}
