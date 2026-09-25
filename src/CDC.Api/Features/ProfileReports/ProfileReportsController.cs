using CDC.Api.Application.Extensions;
using CDC.Api.Features.ProfileReports.Commands;
using CDC.Api.Features.ProfileReports.Dtos;
using CDC.Api.Features.ProfileReports.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.ProfileReports;

/// <summary>
/// Profile reports: generated and persisted report documents. Replaces the legacy
/// <c>IProfileReportService</c> WCF endpoint.
/// </summary>
/// <remarks>
/// Only <see cref="GetProfileVersionReports"/>, <see cref="GetProfileReportData"/> and
/// <see cref="CreateProfileReport"/> are backed by a database call, matching the legacy
/// <c>spgProfileVersionReportByProfileVersionId</c>, <c>spgProfileVersionReportData</c> and
/// <c>spiProfileVersionReportData</c> stored procedures. Every other endpoint mirrors a legacy
/// operation that rendered PDF bytes in-process via TallPDF report classes with no stored
/// procedure at all; TallPDF is end-of-life and is being replaced workspace-wide by a Razor
/// views + Playwright pipeline, so those endpoints return an <c>isAvailable: false</c>
/// descriptor rather than PDF bytes until that pipeline is wired up.
/// </remarks>
/// <param name="mediator">Dispatches queries and commands to their handlers.</param>
[ApiController]
[Route("api/profile-reports")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class ProfileReportsController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Gets the reports available for a profile version.
    /// </summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="isAuthenticated">Whether the caller is authenticated, matching the legacy visibility rule.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The available reports; empty when none are defined.</returns>
    /// <response code="200">The reports were retrieved.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpGet("profile-version/{profileVersionId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<ProfileVersionReportDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ProfileVersionReportDto>>> GetProfileVersionReports(
        Guid profileVersionId,
        [FromQuery] bool isAuthenticated,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfileVersionReportsQuery(profileVersionId, isAuthenticated), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets a previously persisted report document.
    /// </summary>
    /// <param name="reportId">The report definition to read.</param>
    /// <param name="profileVersionId">The profile version the report covers.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The persisted document.</returns>
    /// <response code="200">The report data was retrieved.</response>
    /// <response code="404">No document has been generated for the supplied report.</response>
    [HttpGet("{reportId:guid}")]
    [ProducesResponseType(typeof(ProfileReportDataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfileReportDataDto>> GetProfileReportData(
        Guid reportId,
        [FromQuery] Guid profileVersionId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfileReportDataQuery(profileVersionId, reportId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Persists a generated report document.
    /// </summary>
    /// <remarks>
    /// The caller supplies the already-rendered document bytes; this endpoint only persists
    /// them, exactly as the legacy <c>spiProfileVersionReportData</c> call did once TallPDF had
    /// produced the bytes.
    /// </remarks>
    /// <param name="command">The report to persist.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The persisted report's identifier.</returns>
    /// <response code="201">The report was persisted.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpPost]
    [ProducesResponseType(typeof(CreateProfileReportResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CreateProfileReportResultDto>> CreateProfileReport(
        [FromBody] CreateProfileReportCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        return result.Status == Domain.Common.ResultStatus.Success
            ? CreatedAtAction(
                nameof(GetProfileReportData),
                new { reportId = result.Value.ProfileReportId, profileVersionId = command.ProfileVersionId },
                result.Value)
            : result.ToActionResult(this);
    }

    /// <summary>
    /// Gets a profile's contribution history report descriptor.
    /// </summary>
    /// <param name="profileId">The profile to report on.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The report descriptor.</returns>
    /// <response code="200">The report descriptor was retrieved.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpGet("contributions/{profileId:guid}")]
    [ProducesResponseType(typeof(ContributionsReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ContributionsReportDto>> GetContributionsReport(Guid profileId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetContributionsReportQuery(profileId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets a printable profile section report descriptor.
    /// </summary>
    /// <param name="profileVersionId">The profile version to report on.</param>
    /// <param name="profileSectionId">The section to report on.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The report descriptor.</returns>
    /// <response code="200">The report descriptor was retrieved.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpGet("print-version/{profileVersionId:guid}")]
    [ProducesResponseType(typeof(ProfilePrintVersionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProfilePrintVersionDto>> GetProfilePrintVersion(
        Guid profileVersionId,
        [FromQuery] Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfilePrintVersionQuery(profileVersionId, profileSectionId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets a profile version comparison report descriptor.
    /// </summary>
    /// <param name="sourceVersionId">The earlier profile version being compared.</param>
    /// <param name="targetVersionId">The later profile version being compared.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The report descriptor.</returns>
    /// <response code="200">The report descriptor was retrieved.</response>
    /// <response code="400">The request failed validation, or the source and target versions are the same.</response>
    [HttpGet("comparison")]
    [ProducesResponseType(typeof(ProfileVersionComparisonReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProfileVersionComparisonReportDto>> GetComparisonReport(
        [FromQuery] Guid sourceVersionId,
        [FromQuery] Guid targetVersionId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfileVersionComparisonReportQuery(sourceVersionId, targetVersionId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets a template-driven bespoke report descriptor.
    /// </summary>
    /// <remarks>
    /// A <c>POST</c>, unlike the other report descriptors: the legacy request carries the
    /// selected section/question/guidance lists and a template title, which do not fit a
    /// route or querystring.
    /// </remarks>
    /// <param name="profileVersionId">The profile version to report on.</param>
    /// <param name="query">The selected sections, questions, guidance and template title.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The report descriptor.</returns>
    /// <response code="200">The report descriptor was retrieved.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpPost("bespoke/{profileVersionId:guid}")]
    [ProducesResponseType(typeof(ProfileVersionBespokeReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProfileVersionBespokeReportDto>> GetBespokeReport(
        Guid profileVersionId,
        [FromBody] GetProfileVersionBespokeReportQuery query,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(query with { ProfileVersionId = profileVersionId }, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets a summary profile report descriptor.
    /// </summary>
    /// <param name="profileVersionId">The profile version to report on.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The report descriptor.</returns>
    /// <response code="200">The report descriptor was retrieved.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpGet("summary-profile/{profileVersionId:guid}")]
    [ProducesResponseType(typeof(SummaryProfileReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SummaryProfileReportDto>> GetSummaryProfileReport(Guid profileVersionId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSummaryProfileReportQuery(profileVersionId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets a summary prioritisation report descriptor.
    /// </summary>
    /// <param name="profileVersionId">The profile version to report on.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The report descriptor.</returns>
    /// <response code="200">The report descriptor was retrieved.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpGet("summary-prioritisation/{profileVersionId:guid}")]
    [ProducesResponseType(typeof(SummaryPrioritisationReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SummaryPrioritisationReportDto>> GetSummaryPrioritisationReport(
        Guid profileVersionId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSummaryPrioritisationReportQuery(profileVersionId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets a disease ranking report descriptor.
    /// </summary>
    /// <remarks>
    /// Mirrors the legacy <c>GetProfileRankingReport</c> operation, which selects a report by
    /// <c>reportType</c> and an optional named filter rather than by profile version.
    /// </remarks>
    /// <param name="reportType">The report to retrieve.</param>
    /// <param name="nameOfFilter">The named filter to apply when <paramref name="reportType"/> is <see cref="ProfileRankingReportType.All"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The report descriptor.</returns>
    /// <response code="200">The report descriptor was retrieved.</response>
    /// <response code="400">An unknown report type was supplied.</response>
    [HttpGet("ranking")]
    [ProducesResponseType(typeof(ProfileRankingReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProfileRankingReportDto>> GetRankingReport(
        [FromQuery] ProfileRankingReportType reportType,
        [FromQuery] string? nameOfFilter,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfileRankingReportQuery(reportType, nameOfFilter), cancellationToken);

        return result.ToActionResult(this);
    }
}
