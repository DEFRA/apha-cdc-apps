using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileReports.Dtos;
using CDC.Api.Features.ProfileReports.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileReports.Queries;

/// <summary>Retrieves a previously persisted report document.</summary>
/// <param name="ProfileVersionId">The profile version the report covers.</param>
/// <param name="ProfileReportId">The report definition to read.</param>
public sealed record GetProfileReportDataQuery(Guid ProfileVersionId, Guid ProfileReportId) : IRequest<Result<ProfileReportDataDto>>;

/// <summary>Validates <see cref="GetProfileReportDataQuery"/>.</summary>
public sealed class GetProfileReportDataQueryValidator : AbstractValidator<GetProfileReportDataQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetProfileReportDataQueryValidator"/> class.</summary>
    public GetProfileReportDataQueryValidator()
    {
        RuleFor(query => query.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");

        RuleFor(query => query.ProfileReportId)
            .NotEmpty().WithMessage("A report id is required.");
    }
}

/// <summary>Handles <see cref="GetProfileReportDataQuery"/>.</summary>
/// <param name="profileReportService">Profile report application service.</param>
public sealed class GetProfileReportDataQueryHandler(IProfileReportService profileReportService)
    : IRequestHandler<GetProfileReportDataQuery, Result<ProfileReportDataDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The persisted document, or a not-found result when none has been generated.</returns>
    public async Task<Result<ProfileReportDataDto>> Handle(GetProfileReportDataQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var report = await profileReportService.GetProfileReportDataAsync(request.ProfileVersionId, request.ProfileReportId, cancellationToken);

        return report is null
            ? Result.NotFound<ProfileReportDataDto>($"Report '{request.ProfileReportId}' has not been generated for profile version '{request.ProfileVersionId}'.")
            : Result.Success(report);
    }
}
