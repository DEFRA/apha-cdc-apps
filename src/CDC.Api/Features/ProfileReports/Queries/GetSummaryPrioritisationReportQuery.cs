using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileReports.Dtos;
using CDC.Api.Features.ProfileReports.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileReports.Queries;

/// <summary>Retrieves a summary prioritisation report descriptor.</summary>
/// <param name="ProfileVersionId">The profile version to report on.</param>
public sealed record GetSummaryPrioritisationReportQuery(Guid ProfileVersionId) : IRequest<Result<SummaryPrioritisationReportDto>>;

/// <summary>Validates <see cref="GetSummaryPrioritisationReportQuery"/>.</summary>
public sealed class GetSummaryPrioritisationReportQueryValidator : AbstractValidator<GetSummaryPrioritisationReportQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetSummaryPrioritisationReportQueryValidator"/> class.</summary>
    public GetSummaryPrioritisationReportQueryValidator()
    {
        RuleFor(query => query.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");
    }
}

/// <summary>Handles <see cref="GetSummaryPrioritisationReportQuery"/>.</summary>
/// <param name="profileReportService">Profile report application service.</param>
public sealed class GetSummaryPrioritisationReportQueryHandler(IProfileReportService profileReportService)
    : IRequestHandler<GetSummaryPrioritisationReportQuery, Result<SummaryPrioritisationReportDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report descriptor.</returns>
    public async Task<Result<SummaryPrioritisationReportDto>> Handle(
        GetSummaryPrioritisationReportQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var report = await profileReportService.GetSummaryPrioritisationReportAsync(request.ProfileVersionId, cancellationToken);

        return Result.Success(report);
    }
}
