using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileReports.Dtos;
using CDC.Api.Features.ProfileReports.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileReports.Queries;

/// <summary>Retrieves the reports available for a profile version.</summary>
/// <param name="ProfileVersionId">The profile version to read.</param>
/// <param name="IsAuthenticated">Whether the caller is authenticated, matching the legacy visibility rule.</param>
public sealed record GetProfileVersionReportsQuery(Guid ProfileVersionId, bool IsAuthenticated)
    : IRequest<Result<IReadOnlyList<ProfileVersionReportDto>>>;

/// <summary>Validates <see cref="GetProfileVersionReportsQuery"/>.</summary>
public sealed class GetProfileVersionReportsQueryValidator : AbstractValidator<GetProfileVersionReportsQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetProfileVersionReportsQueryValidator"/> class.</summary>
    public GetProfileVersionReportsQueryValidator()
    {
        RuleFor(query => query.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");
    }
}

/// <summary>Handles <see cref="GetProfileVersionReportsQuery"/>.</summary>
/// <param name="profileReportService">Profile report application service.</param>
public sealed class GetProfileVersionReportsQueryHandler(IProfileReportService profileReportService)
    : IRequestHandler<GetProfileVersionReportsQuery, Result<IReadOnlyList<ProfileVersionReportDto>>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The available reports; empty when none are defined.</returns>
    public async Task<Result<IReadOnlyList<ProfileVersionReportDto>>> Handle(
        GetProfileVersionReportsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reports = await profileReportService.GetProfileVersionReportsAsync(
            request.ProfileVersionId,
            request.IsAuthenticated,
            cancellationToken);

        return Result.Success(reports);
    }
}
