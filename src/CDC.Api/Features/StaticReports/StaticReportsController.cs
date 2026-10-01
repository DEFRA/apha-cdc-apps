using CDC.Api.Application.Extensions;
using CDC.Api.Features.StaticReports.Commands;
using CDC.Api.Features.StaticReports.Dtos;
using CDC.Api.Features.StaticReports.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.StaticReports;

/// <summary>
/// Static reports: general reports and user manuals, each with a version history. Replaces the
/// legacy <c>IStaticReportService</c> WCF endpoint used by <c>StaticReports.aspx</c> and the
/// footer's "D2R2 Quality Statement" link.
/// </summary>
/// <param name="mediator">Dispatches queries and commands to their handlers.</param>
[ApiController]
[Route("api/static-reports")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class StaticReportsController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Gets the current version of every static report.
    /// </summary>
    /// <param name="isUserManual">Whether to retrieve user manuals rather than general reports.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The current version of every matching report.</returns>
    /// <response code="200">The reports were retrieved.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StaticReportVersionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StaticReportVersionDto>>> GetCurrent(
        [FromQuery] bool isUserManual,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetCurrentStaticReportsQuery(isUserManual), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets every version of one static report, most recent first.
    /// </summary>
    /// <param name="staticReportId">The report whose history is being read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every version of the report.</returns>
    /// <response code="200">The history was retrieved.</response>
    [HttpGet("{staticReportId:guid}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<StaticReportVersionDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StaticReportVersionDto>>> GetHistory(
        Guid staticReportId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetStaticReportHistoryQuery(staticReportId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets one static report version's PDF content.
    /// </summary>
    /// <param name="staticReportVersionId">The version to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The PDF content.</returns>
    /// <response code="200">The report data was retrieved.</response>
    /// <response code="404">No version exists with that id.</response>
    [HttpGet("{staticReportVersionId:guid}/data")]
    [ProducesResponseType(typeof(StaticReportDataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StaticReportDataDto>> GetData(Guid staticReportVersionId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetStaticReportDataQuery(staticReportVersionId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Uploads a new static report version, superseding the previous current version for that
    /// title.
    /// </summary>
    /// <param name="request">The title, PDF bytes and visibility for the new version.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The new version was stored.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload([FromBody] UploadStaticReportRequestDto request, CancellationToken cancellationToken)
    {
        var command = new UploadStaticReportCommand(request.Title, request.PdfData, request.IsUserManual, request.IsPublic);
        var result = await mediator.Send(command, cancellationToken);

        return result.ToNoContentActionResult(this);
    }

    /// <summary>
    /// Deletes a current static report version, reinstating the previous version as current if
    /// one exists.
    /// </summary>
    /// <param name="staticReportVersionId">The version to delete.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The version was deleted.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="409">The version is not the current version, so it cannot be deleted.</response>
    [HttpDelete("{staticReportVersionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid staticReportVersionId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteStaticReportVersionCommand(staticReportVersionId), cancellationToken);

        return result.ToNoContentActionResult(this);
    }
}
