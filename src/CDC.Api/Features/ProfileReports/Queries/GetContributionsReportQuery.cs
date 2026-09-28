using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileReports.Dtos;
using CDC.Api.Features.ProfileReports.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileReports.Queries;

/// <summary>Retrieves a profile's contribution history report descriptor.</summary>
/// <param name="ProfileId">The profile to report on.</param>
public sealed record GetContributionsReportQuery(Guid ProfileId) : IRequest<Result<ContributionsReportDto>>;

/// <summary>Validates <see cref="GetContributionsReportQuery"/>.</summary>
public sealed class GetContributionsReportQueryValidator : AbstractValidator<GetContributionsReportQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetContributionsReportQueryValidator"/> class.</summary>
    public GetContributionsReportQueryValidator()
    {
        RuleFor(query => query.ProfileId)
            .NotEmpty().WithMessage("A profile id is required.");
    }
}

/// <summary>Handles <see cref="GetContributionsReportQuery"/>.</summary>
/// <param name="profileReportService">Profile report application service.</param>
public sealed class GetContributionsReportQueryHandler(IProfileReportService profileReportService)
    : IRequestHandler<GetContributionsReportQuery, Result<ContributionsReportDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report descriptor.</returns>
    public async Task<Result<ContributionsReportDto>> Handle(GetContributionsReportQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var report = await profileReportService.GetContributionsReportAsync(request.ProfileId, cancellationToken);

        return Result.Success(report);
    }
}
