using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Common.Contracts;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileReports.Queries;

/// <summary>Retrieves the content the profile report PDFs print for a profile version.</summary>
/// <param name="ProfileVersionId">The profile version to report on.</param>
public sealed record GetProfileReportContentQuery(Guid ProfileVersionId) : IRequest<Result<ProfileReportContentDto>>;

/// <summary>Validates <see cref="GetProfileReportContentQuery"/>.</summary>
public sealed class GetProfileReportContentQueryValidator : AbstractValidator<GetProfileReportContentQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetProfileReportContentQueryValidator"/> class.</summary>
    public GetProfileReportContentQueryValidator()
    {
        RuleFor(query => query.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");
    }
}

/// <summary>Handles <see cref="GetProfileReportContentQuery"/>.</summary>
/// <param name="contentService">Builds the report content.</param>
public sealed class GetProfileReportContentQueryHandler(IProfileReportContentService contentService)
    : IRequestHandler<GetProfileReportContentQuery, Result<ProfileReportContentDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The content.</returns>
    public Task<Result<ProfileReportContentDto>> Handle(GetProfileReportContentQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return contentService.GetProfileReportContentAsync(request.ProfileVersionId, cancellationToken);
    }
}
