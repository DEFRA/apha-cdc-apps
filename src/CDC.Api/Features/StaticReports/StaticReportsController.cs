using CDC.Api.Features.StaticReports.Dtos;
using CDC.Api.Features.StaticReports.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.StaticReports;

/// <summary>
/// Static reports and user manuals: the documents behind the legacy <c>StaticReports.aspx</c>
/// page ("Help using D2R2" when <c>UserManual=1</c>).
/// </summary>
/// <param name="staticReportService">Static report data service.</param>
[ApiController]
[Route("api/static-reports")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class StaticReportsController(IStaticReportService staticReportService) : ControllerBase
{
    /// <summary>
    /// Gets the current version of every static report, or of every user manual.
    /// </summary>
    /// <param name="isUserManual">Return user manuals rather than general reports.</param>
    /// <param name="publicOnly">Restrict to versions marked public.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The reports were retrieved.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StaticReportDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StaticReportDto>>> GetCurrentStaticReports(
        [FromQuery] bool isUserManual = false,
        [FromQuery] bool publicOnly = false,
        CancellationToken cancellationToken = default)
    {
        var reports = await staticReportService.GetCurrentStaticReportsAsync(isUserManual, publicOnly, cancellationToken);

        return Ok(reports);
    }

    /// <summary>
    /// Gets every version of one static report or user manual.
    /// </summary>
    /// <param name="staticReportId">The logical report to read the history of.</param>
    /// <param name="publicOnly">Restrict to versions marked public.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The version history was retrieved.</response>
    [HttpGet("{staticReportId:guid}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<StaticReportDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StaticReportDto>>> GetStaticReportHistory(
        Guid staticReportId,
        [FromQuery] bool publicOnly = false,
        CancellationToken cancellationToken = default)
    {
        var versions = await staticReportService.GetStaticReportHistoryAsync(staticReportId, publicOnly, cancellationToken);

        return Ok(versions);
    }

    /// <summary>
    /// Gets the stored document for one version.
    /// </summary>
    /// <param name="staticReportVersionId">The version to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The document was retrieved.</response>
    /// <response code="404">No document is stored for the supplied version.</response>
    [HttpGet("versions/{staticReportVersionId:guid}/document")]
    [ProducesResponseType(typeof(StaticReportDataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StaticReportDataDto>> GetStaticReportDocument(
        Guid staticReportVersionId,
        CancellationToken cancellationToken)
    {
        var document = await staticReportService.GetStaticReportDataAsync(staticReportVersionId, cancellationToken);

        return document is null ? NotFound() : Ok(document);
    }

    /// <summary>
    /// Deletes one version.
    /// </summary>
    /// <param name="staticReportVersionId">The version to delete.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The version was deleted.</response>
    /// <response code="404">No such version exists.</response>
    [HttpDelete("versions/{staticReportVersionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteStaticReportVersion(
        Guid staticReportVersionId,
        CancellationToken cancellationToken)
    {
        var deleted = await staticReportService.DeleteStaticReportVersionAsync(staticReportVersionId, cancellationToken);

        return deleted ? NoContent() : NotFound();
    }
}
