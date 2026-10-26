using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Api.Features.ProfileSections.Commands;
using CDC.Common.Contracts;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileReports.Queries;

/// <summary>Retrieves the data behind the disease reviews report: the profile sections whose reviews fall due in a date range for one user.</summary>
/// <param name="FromDate">The first day of the range.</param>
/// <param name="ToDate">The last day of the range.</param>
/// <param name="AuthorId">The user the sections are assigned to.</param>
public sealed record GetDiseaseReviewsReportQuery(DateTime FromDate, DateTime ToDate, Guid AuthorId) : IRequest<Result<DiseaseReviewsReportDto>>;

/// <summary>Validates <see cref="GetDiseaseReviewsReportQuery"/>.</summary>
public sealed class GetDiseaseReviewsReportQueryValidator : AbstractValidator<GetDiseaseReviewsReportQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetDiseaseReviewsReportQueryValidator"/> class.</summary>
    public GetDiseaseReviewsReportQueryValidator()
    {
        RuleFor(query => query.AuthorId)
            .NotEmpty().WithMessage("An author id is required.");

        // The review dates are smalldatetime columns, which the database cannot compare with a date outside their range.
        RuleFor(query => query.FromDate).Must(BeWithinSupportedRange)
            .WithMessage($"The from date must be between {SaveProfileVersionRevisionDatesCommandValidator.MinimumReviewDate:d MMMM yyyy} and {SaveProfileVersionRevisionDatesCommandValidator.MaximumReviewDate:d MMMM yyyy}.");
        RuleFor(query => query.ToDate).Must(BeWithinSupportedRange)
            .WithMessage($"The to date must be between {SaveProfileVersionRevisionDatesCommandValidator.MinimumReviewDate:d MMMM yyyy} and {SaveProfileVersionRevisionDatesCommandValidator.MaximumReviewDate:d MMMM yyyy}.");
    }

    private static bool BeWithinSupportedRange(DateTime date) =>
        date.Date >= SaveProfileVersionRevisionDatesCommandValidator.MinimumReviewDate
        && date.Date <= SaveProfileVersionRevisionDatesCommandValidator.MaximumReviewDate;
}

/// <summary>Handles <see cref="GetDiseaseReviewsReportQuery"/>.</summary>
/// <param name="profileReportService">Profile report application service.</param>
public sealed class GetDiseaseReviewsReportQueryHandler(IProfileReportService profileReportService)
    : IRequestHandler<GetDiseaseReviewsReportQuery, Result<DiseaseReviewsReportDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report data, or a forbidden result.</returns>
    public async Task<Result<DiseaseReviewsReportDto>> Handle(GetDiseaseReviewsReportQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return await profileReportService.GetDiseaseReviewsReportAsync(request.FromDate, request.ToDate, request.AuthorId, cancellationToken);
    }
}
