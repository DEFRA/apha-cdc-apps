using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileReports.Dtos;
using CDC.Api.Features.ProfileReports.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileReports.Queries;

/// <summary>Retrieves a profile version comparison report descriptor.</summary>
/// <param name="SourceVersionId">The earlier profile version being compared.</param>
/// <param name="TargetVersionId">The later profile version being compared.</param>
public sealed record GetProfileVersionComparisonReportQuery(Guid SourceVersionId, Guid TargetVersionId)
    : IRequest<Result<ProfileVersionComparisonReportDto>>;

/// <summary>Validates <see cref="GetProfileVersionComparisonReportQuery"/>.</summary>
public sealed class GetProfileVersionComparisonReportQueryValidator : AbstractValidator<GetProfileVersionComparisonReportQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetProfileVersionComparisonReportQueryValidator"/> class.</summary>
    public GetProfileVersionComparisonReportQueryValidator()
    {
        RuleFor(query => query.SourceVersionId)
            .NotEmpty().WithMessage("A source profile version id is required.");

        RuleFor(query => query.TargetVersionId)
            .NotEmpty().WithMessage("A target profile version id is required.");

        RuleFor(query => query)
            .Must(query => query.SourceVersionId != query.TargetVersionId)
            .WithMessage("The source and target profile versions cannot be the same.")
            .When(query => query.SourceVersionId != Guid.Empty && query.TargetVersionId != Guid.Empty);
    }
}

/// <summary>Handles <see cref="GetProfileVersionComparisonReportQuery"/>.</summary>
/// <param name="profileReportService">Profile report application service.</param>
public sealed class GetProfileVersionComparisonReportQueryHandler(IProfileReportService profileReportService)
    : IRequestHandler<GetProfileVersionComparisonReportQuery, Result<ProfileVersionComparisonReportDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report descriptor.</returns>
    public async Task<Result<ProfileVersionComparisonReportDto>> Handle(
        GetProfileVersionComparisonReportQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var report = await profileReportService.GetProfileVersionComparisonReportAsync(
            request.SourceVersionId,
            request.TargetVersionId,
            cancellationToken);

        return Result.Success(report);
    }
}
