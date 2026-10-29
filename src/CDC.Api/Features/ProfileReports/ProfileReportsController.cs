using CDC.Api.Application.Extensions;
using CDC.Api.Features.ProfileReports.Commands;
using CDC.Api.Features.ProfileReports.Dtos;
using CDC.Api.Features.ProfileReports.Queries;
using CDC.Common.Contracts;
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
    /// Gets what the "Profile reports" page shows for a profile version.
    /// </summary>
    /// <remarks>
    /// Replaces the legacy <c>ProfileReports.aspx</c> data sources: the version's title and number,
    /// whether the user may regenerate its reports, and the reports (without the print-only full profile).
    /// </remarks>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The overview.</returns>
    /// <response code="200">The overview was retrieved.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="404">The profile version does not exist.</response>
    [HttpGet("overview/{profileVersionId:guid}")]
    [ProducesResponseType(typeof(ProfileReportsOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfileReportsOverviewDto>> GetProfileReportsOverview(Guid profileVersionId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfileReportsOverviewQuery(profileVersionId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets the display-ready content the profile report PDFs print for a profile version.
    /// </summary>
    /// <remarks>
    /// Replaces the CSLA business objects the legacy TallPDF report classes (full profile, summary
    /// profile, Q&amp;A brief, summary prioritisation, bespoke) loaded: every section's questions with
    /// answers laid out as standard, repeating or per-species groups, list options resolved to
    /// ticked and unticked labels, and relevance applied. CDC.Web renders it with Razor views.
    /// </remarks>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The report content.</returns>
    /// <response code="200">The content was retrieved.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="404">The profile version does not exist.</response>
    [HttpGet("content/{profileVersionId:guid}")]
    [ProducesResponseType(typeof(ProfileReportContentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfileReportContentDto>> GetProfileReportContent(Guid profileVersionId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfileReportContentQuery(profileVersionId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets the prioritisation scores the summary prioritisation report prints for a profile version.
    /// </summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The scores; empty lists when prioritisation has not been run for the version.</returns>
    /// <response code="200">The scores were retrieved.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="404">The profile version does not exist.</response>
    [HttpGet("prioritisation/{profileVersionId:guid}")]
    [ProducesResponseType(typeof(ProfilePrioritisationScoresDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfilePrioritisationScoresDto>> GetProfilePrioritisationScores(Guid profileVersionId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfilePrioritisationScoresQuery(profileVersionId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets the user guidance of a set of questions.
    /// </summary>
    /// <remarks>
    /// A <c>POST</c> because the selected questions of a bespoke report do not fit a route or
    /// querystring. It reads, and changes nothing. The legacy bespoke report looked each selected
    /// question up with <c>ProfileQuestion.GetProfileQuestion</c> to print its guidance table.
    /// </remarks>
    /// <param name="request">The questions whose guidance is wanted.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The guidance of each question that exists.</returns>
    /// <response code="200">The guidance was retrieved.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpPost("guidance")]
    [ProducesResponseType(typeof(IReadOnlyList<ProfileQuestionGuidanceDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ProfileQuestionGuidanceDto>>> GetQuestionGuidance(
        [FromBody] ProfileQuestionGuidanceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfileQuestionGuidanceQuery(request.QuestionIds), cancellationToken);

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
    /// Gets the data behind a profile's contributions report.
    /// </summary>
    /// <remarks>
    /// Replaces the legacy <c>GetContributionsReport</c> operation, which rendered a PDF with TallPDF:
    /// this returns the report's content (every draft version and each section's contributions) and
    /// CDC.Web renders it to PDF with a Razor view and Playwright.
    /// </remarks>
    /// <param name="profileId">The profile to report on.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The report data.</returns>
    /// <response code="200">The report data was retrieved.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="403">The user may not run the contributions report.</response>
    /// <response code="404">The profile has no draft versions.</response>
    [HttpGet("contributions/{profileId:guid}")]
    [ProducesResponseType(typeof(ContributionsReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContributionsReportDto>> GetContributionsReport(Guid profileId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetContributionsReportQuery(profileId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets the data behind the disease reviews report.
    /// </summary>
    /// <remarks>
    /// Replaces the legacy <c>GetProfileDiseaseReviewsReport</c> operation, which rendered a PDF with TallPDF:
    /// this returns the profile sections whose author, VA or policy review falls due in the date range
    /// (<c>spgaDiseaseReviews</c>) and CDC.Web renders them to PDF with a Razor view and Playwright.
    /// </remarks>
    /// <param name="fromDate">The first day of the range.</param>
    /// <param name="toDate">The last day of the range.</param>
    /// <param name="authorId">The user the sections are assigned to.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The report data; no sections when no review is due.</returns>
    /// <response code="200">The report data was retrieved.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="403">The user may not run the disease reviews report.</response>
    [HttpGet("disease-reviews")]
    [ProducesResponseType(typeof(DiseaseReviewsReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DiseaseReviewsReportDto>> GetDiseaseReviewsReport(
        [FromQuery] DateTime fromDate,
        [FromQuery] DateTime toDate,
        [FromQuery] Guid authorId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetDiseaseReviewsReportQuery(fromDate, toDate, authorId), cancellationToken);

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
