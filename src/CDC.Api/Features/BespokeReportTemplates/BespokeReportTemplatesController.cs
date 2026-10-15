using CDC.Api.Application.Extensions;
using CDC.Api.Features.BespokeReportTemplates.Commands;
using CDC.Api.Features.BespokeReportTemplates.Queries;
using CDC.Common.Contracts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.BespokeReportTemplates;

/// <summary>
/// Saved bespoke report templates: a title and the sections, questions and guidance of a profile
/// that were ticked on the "Create bespoke report" page. Replaces the legacy
/// <c>IBespokeReportTemplateService</c> WCF endpoint.
/// </summary>
/// <param name="mediator">Dispatches queries and commands to their handlers.</param>
[ApiController]
[Route("api/bespoke-report-templates")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class BespokeReportTemplatesController(ISender mediator) : ControllerBase
{
    /// <summary>Lists the saved templates, ordered by title.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The templates, without their ticked items.</returns>
    /// <response code="200">The templates were retrieved.</response>
    /// <response code="403">The user may not view templates.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BespokeReportTemplateSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<BespokeReportTemplateSummaryDto>>> GetTemplates(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetBespokeReportTemplatesQuery(), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Gets one template with its ticked items.</summary>
    /// <param name="id">The template to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The template.</returns>
    /// <response code="200">The template was retrieved.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="403">The user may not view templates.</response>
    /// <response code="404">The template does not exist.</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BespokeReportTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BespokeReportTemplateDto>> GetTemplate(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetBespokeReportTemplateQuery(id), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Saves the ticked items as a new template ("Save template as").</summary>
    /// <param name="request">The new template's title and ticked items.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The saved template.</returns>
    /// <response code="201">The template was saved.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="403">The user may not change templates.</response>
    /// <response code="409">Another template already has the title.</response>
    [HttpPost]
    [ProducesResponseType(typeof(BespokeReportTemplateDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BespokeReportTemplateDto>> SaveTemplateAs(
        [FromBody] SaveBespokeReportTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new SaveBespokeReportTemplateCommand(request.Title, request.CheckedList), cancellationToken);

        return result.Status == Domain.Common.ResultStatus.Success
            ? CreatedAtAction(nameof(GetTemplate), new { id = result.Value.Id }, result.Value)
            : result.ToActionResult(this);
    }

    /// <summary>Replaces a template's ticked items ("Update template").</summary>
    /// <param name="id">The template to change.</param>
    /// <param name="request">The ticked items to save.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The saved template.</returns>
    /// <response code="200">The template was saved.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="403">The user may not change templates.</response>
    /// <response code="404">The template does not exist.</response>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(BespokeReportTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BespokeReportTemplateDto>> UpdateTemplate(
        Guid id,
        [FromBody] UpdateBespokeReportTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateBespokeReportTemplateCommand(id, request.CheckedList), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Changes a template's title ("Edit title").</summary>
    /// <param name="id">The template to change.</param>
    /// <param name="request">The new title.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The saved template.</returns>
    /// <response code="200">The title was saved.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="403">The user may not change templates.</response>
    /// <response code="404">The template does not exist.</response>
    /// <response code="409">Another template already has the title.</response>
    [HttpPut("{id:guid}/title")]
    [ProducesResponseType(typeof(BespokeReportTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BespokeReportTemplateDto>> RenameTemplate(
        Guid id,
        [FromBody] RenameBespokeReportTemplateRequest request,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new RenameBespokeReportTemplateCommand(id, request.Title), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>Deletes a template.</summary>
    /// <param name="id">The template to delete.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>No content.</returns>
    /// <response code="204">The template was deleted.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="403">The user may not change templates.</response>
    /// <response code="404">The template does not exist.</response>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteTemplate(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteBespokeReportTemplateCommand(id), cancellationToken);

        return result.ToNoContentActionResult(this);
    }
}
