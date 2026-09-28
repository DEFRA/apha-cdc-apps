using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileQuestions.Dtos;
using CDC.Api.Features.ProfileQuestions.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileQuestions.Queries;

/// <summary>Retrieves a profile guidance report descriptor.</summary>
/// <param name="ReportType">The report to retrieve.</param>
public sealed record GetProfileGuidanceReportQuery(ProfileGuidanceReportType ReportType) : IRequest<Result<ProfileGuidanceReportDto>>;

/// <summary>Validates <see cref="GetProfileGuidanceReportQuery"/>.</summary>
public sealed class GetProfileGuidanceReportQueryValidator : AbstractValidator<GetProfileGuidanceReportQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetProfileGuidanceReportQueryValidator"/> class.</summary>
    public GetProfileGuidanceReportQueryValidator()
    {
        RuleFor(query => query.ReportType)
            .IsInEnum().WithMessage("An unknown report type was supplied.");
    }
}

/// <summary>Handles <see cref="GetProfileGuidanceReportQuery"/>.</summary>
/// <param name="profileQuestionService">Profile question application service.</param>
public sealed class GetProfileGuidanceReportQueryHandler(IProfileQuestionService profileQuestionService)
    : IRequestHandler<GetProfileGuidanceReportQuery, Result<ProfileGuidanceReportDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report descriptor.</returns>
    public async Task<Result<ProfileGuidanceReportDto>> Handle(GetProfileGuidanceReportQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var report = await profileQuestionService.GetProfileGuidanceReportAsync(request.ReportType, cancellationToken);

        return Result.Success(report);
    }
}
