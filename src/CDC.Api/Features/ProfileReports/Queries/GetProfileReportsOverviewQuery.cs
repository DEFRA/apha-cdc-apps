using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Common.Contracts;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileReports.Queries;

/// <summary>Retrieves what the "Profile reports" page shows for a profile version.</summary>
/// <param name="ProfileVersionId">The profile version to read.</param>
public sealed record GetProfileReportsOverviewQuery(Guid ProfileVersionId) : IRequest<Result<ProfileReportsOverviewDto>>;

/// <summary>Validates <see cref="GetProfileReportsOverviewQuery"/>.</summary>
public sealed class GetProfileReportsOverviewQueryValidator : AbstractValidator<GetProfileReportsOverviewQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetProfileReportsOverviewQueryValidator"/> class.</summary>
    public GetProfileReportsOverviewQueryValidator()
    {
        RuleFor(query => query.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");
    }
}

/// <summary>Handles <see cref="GetProfileReportsOverviewQuery"/>.</summary>
/// <param name="profileReportService">Profile report application service.</param>
public sealed class GetProfileReportsOverviewQueryHandler(IProfileReportService profileReportService)
    : IRequestHandler<GetProfileReportsOverviewQuery, Result<ProfileReportsOverviewDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The overview.</returns>
    public Task<Result<ProfileReportsOverviewDto>> Handle(GetProfileReportsOverviewQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return profileReportService.GetProfileReportsOverviewAsync(request.ProfileVersionId, cancellationToken);
    }
}
