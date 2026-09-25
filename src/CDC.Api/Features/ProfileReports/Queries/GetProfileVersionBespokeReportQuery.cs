using System.Text.Json.Serialization;
using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileReports.Dtos;
using CDC.Api.Features.ProfileReports.Interfaces;
using FluentValidation;
using MediatR;

namespace CDC.Api.Features.ProfileReports.Queries;

/// <summary>Retrieves a template-driven bespoke report descriptor.</summary>
/// <param name="ProfileVersionId">The profile version to report on.</param>
/// <param name="SelectedSections">The sections to include.</param>
/// <param name="SelectedQuestions">The questions to include.</param>
/// <param name="SelectedGuidance">The guidance to include.</param>
/// <param name="TemplateTitle">The template's display title.</param>
public sealed record GetProfileVersionBespokeReportQuery(
    [property: JsonRequired] Guid ProfileVersionId,
    IReadOnlyList<string> SelectedSections,
    IReadOnlyList<string> SelectedQuestions,
    IReadOnlyList<string> SelectedGuidance,
    string TemplateTitle) : IRequest<Result<ProfileVersionBespokeReportDto>>;

/// <summary>Validates <see cref="GetProfileVersionBespokeReportQuery"/>.</summary>
public sealed class GetProfileVersionBespokeReportQueryValidator : AbstractValidator<GetProfileVersionBespokeReportQuery>
{
    /// <summary>Initialises a new instance of the <see cref="GetProfileVersionBespokeReportQueryValidator"/> class.</summary>
    public GetProfileVersionBespokeReportQueryValidator()
    {
        RuleFor(query => query.ProfileVersionId)
            .NotEmpty().WithMessage("A profile version id is required.");

        RuleFor(query => query.TemplateTitle)
            .NotEmpty().WithMessage("A template title is required.");

        RuleFor(query => query)
            .Must(query => query.SelectedSections.Count > 0 || query.SelectedQuestions.Count > 0 || query.SelectedGuidance.Count > 0)
            .WithMessage("At least one section, question or guidance item must be selected.");
    }
}

/// <summary>Handles <see cref="GetProfileVersionBespokeReportQuery"/>.</summary>
/// <param name="profileReportService">Profile report application service.</param>
public sealed class GetProfileVersionBespokeReportQueryHandler(IProfileReportService profileReportService)
    : IRequestHandler<GetProfileVersionBespokeReportQuery, Result<ProfileVersionBespokeReportDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report descriptor.</returns>
    public async Task<Result<ProfileVersionBespokeReportDto>> Handle(
        GetProfileVersionBespokeReportQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var report = await profileReportService.GetProfileVersionBespokeReportAsync(
            request.ProfileVersionId,
            request.SelectedSections,
            request.SelectedQuestions,
            request.SelectedGuidance,
            request.TemplateTitle,
            cancellationToken);

        return Result.Success(report);
    }
}
