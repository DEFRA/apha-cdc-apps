using CDC.Api.Domain.Common;
using CDC.Api.Features.StaticReports.Dtos;
using CDC.Api.Features.StaticReports.Interfaces;
using MediatR;

namespace CDC.Api.Features.StaticReports.Queries;

/// <summary>
/// Retrieves one static report version's PDF content.
/// </summary>
/// <param name="StaticReportVersionId">The version to read.</param>
public sealed record GetStaticReportDataQuery(Guid StaticReportVersionId) : IRequest<Result<StaticReportDataDto>>;

/// <summary>
/// Handles <see cref="GetStaticReportDataQuery"/>.
/// </summary>
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
        var data = await repository.GetDataAsync(request.StaticReportVersionId, cancellationToken);

        if (data is null)
        {
            return Result.NotFound<StaticReportDataDto>($"No static report version exists with id '{request.StaticReportVersionId}'.");
        }

        return Result.Success(new StaticReportDataDto
        {
            PdfData = data.PdfData,
            Title = data.Title
        });
    }
}
