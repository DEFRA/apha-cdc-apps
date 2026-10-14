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
    /// Gets whether the current user may upload a new static report or user manual.
    /// </summary>
    /// <response code="200">The permission was retrieved.</response>
    [HttpGet("upload-permission")]
    [ProducesResponseType(typeof(StaticReportUploadPermissionDto), StatusCodes.Status200OK)]
    public ActionResult<StaticReportUploadPermissionDto> GetUploadPermission() =>
        Ok(new StaticReportUploadPermissionDto { CanUpload = staticReportService.CanUploadStaticReports });

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
    /// Gets the stored document for one version as a PDF file.
    /// </summary>
    /// <param name="staticReportVersionId">The version to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="200">The document was retrieved.</response>
    /// <response code="404">No document is stored for the supplied version.</response>
    [HttpGet("versions/{staticReportVersionId:guid}/document")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStaticReportDocument(
        Guid staticReportVersionId,
        CancellationToken cancellationToken)
    {
        var document = await staticReportService.GetStaticReportDataAsync(staticReportVersionId, cancellationToken);

        if (document is null)
        {
            return NotFound();
        }

        // "inline", not "attachment" - and set via the header directly, not File()'s
        // fileDownloadName parameter (which always forces "attachment") - so browsers render the
        // PDF rather than downloading it.
        var fileName = document.Title.Replace("\"", string.Empty, StringComparison.Ordinal);
        Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}.pdf\"";

        return File(document.PdfData, "application/pdf", enableRangeProcessing: true);
    }

    /// <summary>
    /// Uploads a new version of a static report or user manual.
    /// </summary>
    /// <param name="request">The title, PDF bytes, and visibility flags for the new version.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The version was uploaded.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="403">The current user may not upload documents.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UploadStaticReport(
        [FromBody] UploadStaticReportRequestDto request,
        CancellationToken cancellationToken)
    {
        var result = await staticReportService.UploadStaticReportAsync(request, cancellationToken);

        return result.Outcome switch
        {
            UploadStaticReportOutcome.Success => NoContent(),
            UploadStaticReportOutcome.Forbidden => Problem(result.ErrorMessage, statusCode: StatusCodes.Status403Forbidden),
            _ => Problem(result.ErrorMessage, statusCode: StatusCodes.Status400BadRequest)
        };
    }

    /// <summary>
    /// Deletes one version.
    /// </summary>
    /// <param name="staticReportVersionId">The version to delete.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <response code="204">The version was deleted.</response>
    /// <response code="403">The current user may not delete this version.</response>
    /// <response code="404">No such version exists.</response>
    [HttpDelete("versions/{staticReportVersionId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteStaticReportVersion(
        Guid staticReportVersionId,
        CancellationToken cancellationToken)
    {
        var outcome = await staticReportService.DeleteStaticReportVersionAsync(staticReportVersionId, cancellationToken);

        return outcome switch
        {
            DeleteStaticReportVersionOutcome.Success => NoContent(),
            DeleteStaticReportVersionOutcome.Forbidden => Problem(
                "You do not have permission to delete this document.", statusCode: StatusCodes.Status403Forbidden),
            _ => NotFound()
        };
    }
}
