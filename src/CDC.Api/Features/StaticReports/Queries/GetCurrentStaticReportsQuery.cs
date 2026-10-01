using CDC.Api.Domain.Common;
using CDC.Api.Features.StaticReports.Dtos;
using CDC.Api.Features.StaticReports.Interfaces;
using MediatR;

namespace CDC.Api.Features.StaticReports.Queries;

/// <summary>
/// Retrieves the current version of every static report, optionally filtered to user manuals.
/// </summary>
/// <param name="IsUserManual">Whether to retrieve user manuals rather than general reports.</param>
public sealed record GetCurrentStaticReportsQuery(bool IsUserManual) : IRequest<Result<IReadOnlyList<StaticReportVersionDto>>>;

/// <summary>
/// Handles <see cref="GetCurrentStaticReportsQuery"/>.
/// </summary>
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
        var versions = await repository.GetCurrentAsync(request.IsUserManual, cancellationToken);

        IReadOnlyList<StaticReportVersionDto> dtos = [.. versions.Select(version => new StaticReportVersionDto
        {
            Id = version.Id,
            StaticReportId = version.StaticReportId,
            Title = version.Title,
            VersionMajor = version.VersionMajor,
            EffectiveDateFrom = version.EffectiveDateFrom,
            EffectiveDateTo = version.EffectiveDateTo,
            IsCurrent = version.IsCurrent,
            IsUserManual = version.IsUserManual,
            IsPublic = version.IsPublic,
            FileSize = version.FileSize
        })];

        return Result.Success(dtos);
    }
}
