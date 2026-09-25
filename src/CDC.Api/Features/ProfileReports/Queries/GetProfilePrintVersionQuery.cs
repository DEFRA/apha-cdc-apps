using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileReports.Dtos;
using CDC.Api.Features.ProfileReports.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileReports.Queries;

/// <summary>Retrieves a printable profile section report descriptor.</summary>
/// <param name="ProfileVersionId">The profile version to report on.</param>
/// <param name="ProfileSectionId">The section to report on.</param>
public sealed record GetProfilePrintVersionQuery(Guid ProfileVersionId, Guid ProfileSectionId) : IRequest<Result<ProfilePrintVersionDto>>;

/// <summary>Validates <see cref="GetProfilePrintVersionQuery"/>.</summary>
public sealed class GetProfilePrintVersionQueryValidator : AbstractValidator<GetProfilePrintVersionQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetProfilePrintVersionQueryValidator"/> class.</summary>
    public GetProfilePrintVersionQueryValidator()
    {
        RuleFor(query => query.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");

        RuleFor(query => query.ProfileSectionId)
            .NotEmpty().WithMessage("A profile section id is required.");
    }
}

/// <summary>Handles <see cref="GetProfilePrintVersionQuery"/>.</summary>
/// <param name="profileReportService">Profile report application service.</param>
public sealed class GetProfilePrintVersionQueryHandler(IProfileReportService profileReportService)
    : IRequestHandler<GetProfilePrintVersionQuery, Result<ProfilePrintVersionDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report descriptor.</returns>
    public async Task<Result<ProfilePrintVersionDto>> Handle(GetProfilePrintVersionQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var report = await profileReportService.GetProfilePrintVersionAsync(
            request.ProfileVersionId,
            request.ProfileSectionId,
            cancellationToken);

        return Result.Success(report);
    }
}
