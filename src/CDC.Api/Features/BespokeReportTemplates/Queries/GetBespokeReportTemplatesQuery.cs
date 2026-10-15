using CDC.Api.Domain.Common;
using CDC.Api.Features.BespokeReportTemplates.Interfaces;
using CDC.Common.Contracts;
using MediatR;

namespace CDC.Api.Features.BespokeReportTemplates.Queries;

/// <summary>Lists the saved bespoke report templates, ordered by title.</summary>
public sealed record GetBespokeReportTemplatesQuery : IRequest<Result<IReadOnlyList<BespokeReportTemplateSummaryDto>>>;

/// <summary>Handles <see cref="GetBespokeReportTemplatesQuery"/>.</summary>
/// <param name="templateService">Bespoke report template application service.</param>
public sealed class GetBespokeReportTemplatesQueryHandler(IBespokeReportTemplateService templateService)
    : IRequestHandler<GetBespokeReportTemplatesQuery, Result<IReadOnlyList<BespokeReportTemplateSummaryDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The templates.</returns>
    public Task<Result<IReadOnlyList<BespokeReportTemplateSummaryDto>>> Handle(GetBespokeReportTemplatesQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return templateService.GetTemplatesAsync(cancellationToken);
    }
}
