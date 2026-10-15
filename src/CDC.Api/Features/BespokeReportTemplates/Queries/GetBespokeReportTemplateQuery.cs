using CDC.Api.Domain.Common;
using CDC.Api.Features.BespokeReportTemplates.Interfaces;
using CDC.Common.Contracts;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.BespokeReportTemplates.Queries;

/// <summary>Retrieves one saved bespoke report template with its ticked items.</summary>
/// <param name="Id">The template to read.</param>
public sealed record GetBespokeReportTemplateQuery(Guid Id) : IRequest<Result<BespokeReportTemplateDto>>;

/// <summary>Validates <see cref="GetBespokeReportTemplateQuery"/>.</summary>
public sealed class GetBespokeReportTemplateQueryValidator : AbstractValidator<GetBespokeReportTemplateQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetBespokeReportTemplateQueryValidator"/> class.</summary>
    public GetBespokeReportTemplateQueryValidator()
    {
        RuleFor(query => query.Id)
            .NotEmpty().WithMessage("A template id is required.");
    }
}

/// <summary>Handles <see cref="GetBespokeReportTemplateQuery"/>.</summary>
/// <param name="templateService">Bespoke report template application service.</param>
public sealed class GetBespokeReportTemplateQueryHandler(IBespokeReportTemplateService templateService)
    : IRequestHandler<GetBespokeReportTemplateQuery, Result<BespokeReportTemplateDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The template.</returns>
    public Task<Result<BespokeReportTemplateDto>> Handle(GetBespokeReportTemplateQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return templateService.GetTemplateAsync(request.Id, cancellationToken);
    }
}
