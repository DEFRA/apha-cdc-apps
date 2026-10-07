using CDC.Api.Domain.Common;
using CDC.Api.Features.StaticReports.Dtos;
using CDC.Api.Features.StaticReports.Interfaces;
using CDC.Api.Features.StaticReports.Mapping;
using MediatR;

namespace CDC.Api.Features.StaticReports.Queries;

/// <summary>Handles <see cref="GetStaticReportDataQuery"/>.</summary>
/// <param name="repository">Static reports data access.</param>
public sealed class GetStaticReportDataQueryHandler(IStaticReportRepository repository)
    : IRequestHandler<GetStaticReportDataQuery, Result<StaticReportDataDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The PDF content, or a not-found result when the version does not exist.</returns>
    public async Task<Result<StaticReportDataDto>> Handle(GetStaticReportDataQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var data = await repository.GetDataAsync(request.StaticReportVersionId, cancellationToken);

        return data is null
            ? Result.NotFound<StaticReportDataDto>($"Static report version '{request.StaticReportVersionId}' was not found.")
            : Result.Success(data.ToDto());
    }
}
