using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileReports.Dtos;
using CDC.Api.Features.ProfileReports.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileReports.Queries;

/// <summary>Retrieves a disease ranking report descriptor.</summary>
/// <param name="ReportType">The report to retrieve.</param>
/// <param name="NameOfFilter">The named filter to apply when <paramref name="ReportType"/> is <see cref="ProfileRankingReportType.All"/>.</param>
public sealed record GetProfileRankingReportQuery(ProfileRankingReportType ReportType, string? NameOfFilter)
    : IRequest<Result<ProfileRankingReportDto>>;

/// <summary>Validates <see cref="GetProfileRankingReportQuery"/>.</summary>
public sealed class GetProfileRankingReportQueryValidator : AbstractValidator<GetProfileRankingReportQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetProfileRankingReportQueryValidator"/> class.</summary>
    public GetProfileRankingReportQueryValidator()
    {
        RuleFor(query => query.ReportType)
            .IsInEnum().WithMessage("An unknown report type was supplied.");
    }
}

/// <summary>Handles <see cref="GetProfileRankingReportQuery"/>.</summary>
/// <param name="profileReportService">Profile report application service.</param>
public sealed class GetProfileRankingReportQueryHandler(IProfileReportService profileReportService)
    : IRequestHandler<GetProfileRankingReportQuery, Result<ProfileRankingReportDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report descriptor.</returns>
    public async Task<Result<ProfileRankingReportDto>> Handle(GetProfileRankingReportQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var report = await profileReportService.GetProfileRankingReportAsync(request.ReportType, request.NameOfFilter, cancellationToken);

        return Result.Success(report);
    }
}
