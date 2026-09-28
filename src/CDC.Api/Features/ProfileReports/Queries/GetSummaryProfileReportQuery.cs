using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileReports.Dtos;
using CDC.Api.Features.ProfileReports.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileReports.Queries;

/// <summary>Retrieves a summary profile report descriptor.</summary>
/// <param name="ProfileVersionId">The profile version to report on.</param>
public sealed record GetSummaryProfileReportQuery(Guid ProfileVersionId) : IRequest<Result<SummaryProfileReportDto>>;

/// <summary>Validates <see cref="GetSummaryProfileReportQuery"/>.</summary>
public sealed class GetSummaryProfileReportQueryValidator : AbstractValidator<GetSummaryProfileReportQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetSummaryProfileReportQueryValidator"/> class.</summary>
    public GetSummaryProfileReportQueryValidator()
    {
        RuleFor(query => query.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");
    }
}

/// <summary>Handles <see cref="GetSummaryProfileReportQuery"/>.</summary>
/// <param name="profileReportService">Profile report application service.</param>
public sealed class GetSummaryProfileReportQueryHandler(IProfileReportService profileReportService)
    : IRequestHandler<GetSummaryProfileReportQuery, Result<SummaryProfileReportDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report descriptor.</returns>
    public async Task<Result<SummaryProfileReportDto>> Handle(GetSummaryProfileReportQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var report = await profileReportService.GetSummaryProfileReportAsync(request.ProfileVersionId, cancellationToken);

        return Result.Success(report);
    }
}
