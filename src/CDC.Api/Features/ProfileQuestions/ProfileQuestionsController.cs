using CDC.Api.Application.Extensions;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileQuestions.Commands;
using CDC.Api.Features.ProfileQuestions.Dtos;
using CDC.Api.Features.ProfileQuestions.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.ProfileQuestions;

/// <summary>
/// Profile questions: questionnaire questions and their guidance text. Replaces the legacy
/// <c>IProfileQuestionService</c> WCF endpoint.
/// </summary>
/// <param name="mediator">Dispatches queries and commands to their handlers.</param>
[ApiController]
[Route("api/profile-questions")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class ProfileQuestionsController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Gets one profile question.
    /// </summary>
    /// <param name="profileQuestionId">The question to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The question.</returns>
    /// <response code="200">The question was retrieved.</response>
    /// <response code="404">No question exists with the supplied identifier.</response>
    [HttpGet("{profileQuestionId:guid}")]
    [ProducesResponseType(typeof(ProfileQuestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProfileQuestionDto>> GetProfileQuestion(Guid profileQuestionId, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfileQuestionQuery(profileQuestionId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets the questions within one profile section.
    /// </summary>
    /// <remarks>
    /// Mirrors the legacy <c>GetProfileQuestionInfoList</c> operation, which is keyed by
    /// section rather than parameterless.
    /// </remarks>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The section's questions; empty when it has none.</returns>
    /// <response code="200">The questions were retrieved.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ProfileQuestionInfoDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ProfileQuestionInfoDto>>> GetProfileQuestionInfoList(
        [FromQuery] Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfileQuestionInfoListQuery(profileSectionId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets a profile guidance report descriptor.
    /// </summary>
    /// <remarks>
    /// Mirrors the legacy <c>GetProfileGuidanceReport</c> operation, which selects a report by
    /// <c>reportType</c> rather than by a specific question. Legacy generated the report's PDF
    /// bytes in-process via TallPDF; TallPDF is end-of-life and is being replaced
    /// workspace-wide by a Razor views + Playwright pipeline, so <c>isAvailable</c> is
    /// <c>false</c> until that pipeline is wired up - no PDF bytes are returned here.
    /// </remarks>
    /// <param name="reportType">The report to retrieve.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The report descriptor.</returns>
    /// <response code="200">The report descriptor was retrieved.</response>
    /// <response code="400">An unknown report type was supplied.</response>
    [HttpGet("guidance-report")]
    [ProducesResponseType(typeof(ProfileGuidanceReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProfileGuidanceReportDto>> GetProfileGuidanceReport(
        [FromQuery] ProfileGuidanceReportType reportType,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfileGuidanceReportQuery(reportType), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Updates a question's guidance text and display names.
    /// </summary>
    /// <remarks>
    /// The question's short name and position within its section are immutable - the legacy
    /// stored procedure never accepted them either - so only <c>name</c>,
    /// <c>nonTechnicalName</c> and <c>userGuidance</c> can be changed. Supply the
    /// <c>lastUpdated</c> row version returned by <c>GET /api/profile-questions/{id}</c>; if
    /// another user has saved in the meantime the request is rejected with 409.
    /// </remarks>
    /// <param name="profileQuestionId">The question to update.</param>
    /// <param name="command">The changes to apply.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The updated question.</returns>
    /// <response code="200">The question was updated.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="404">No question exists with the supplied identifier.</response>
    /// <response code="409">Another user has saved this question since it was read.</response>
    [HttpPut("{profileQuestionId:guid}")]
    [ProducesResponseType(typeof(ProfileQuestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProfileQuestionDto>> UpdateProfileQuestion(
        Guid profileQuestionId,
        [FromBody] UpdateProfileQuestionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command with { Id = profileQuestionId }, cancellationToken);

        return result.ToActionResult(this);
    }
}
