using CDC.Api.Application.Extensions;
using CDC.Api.Features.ProfileSections.Commands;
using CDC.Api.Features.ProfileSections.Queries;
using CDC.Common.Contracts;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.ProfileSections;

/// <summary>
/// Profile reference sections: the questionnaire structure (sections, questions and fields) and
/// the recorded answers for a profile version. Replaces the legacy <c>EditProfileQuestions.aspx</c>
/// page's data access.
/// </summary>
/// <param name="mediator">Dispatches queries to their handlers.</param>
[ApiController]
[Route("api/profile-sections")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class ProfileSectionsController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Gets the profile questionnaire structure.
    /// </summary>
    /// <remarks>
    /// Returns the 16 fixed profile reference sections (Summary, Epidemiology, and so on), their
    /// questions, and each question's fields. The structure is the same for every profile;
    /// answers are retrieved separately via <c>GET /api/profile-sections/answers</c>.
    ///
    /// Sample response:
    ///
    ///     {
    ///       "sections": [
    ///         {
    ///           "id": "1f2e3d4c-5b6a-7988-9a0b-1c2d3e4f5a6b",
    ///           "name": "Epidemiology",
    ///           "sectionNumber": 3,
    ///           "questions": [ { "id": "...", "questionNumber": 1, "fields": [ { "id": "...", "dataTypeName": "List" } ] } ]
    ///         }
    ///       ]
    ///     }
    /// </remarks>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Sections, questions and fields.</returns>
    /// <response code="200">The metadata was retrieved.</response>
    [HttpGet("metadata")]
    [ProducesResponseType(typeof(ProfileQuestionnaireMetadataDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProfileQuestionnaireMetadataDto>> GetProfileQuestionnaireMetadata(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfileQuestionnaireMetadataQuery(), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets the recorded answers for one profile version's reference section.
    /// </summary>
    /// <remarks>
    /// Sample request:
    ///
    ///     GET /api/profile-sections/answers?profileVersionId=6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f&amp;profileSectionId=1f2e3d4c-5b6a-7988-9a0b-1c2d3e4f5a6b
    ///
    /// Sample response:
    ///
    ///     {
    ///       "profileVersionId": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
    ///       "profileSectionId": "1f2e3d4c-5b6a-7988-9a0b-1c2d3e4f5a6b",
    ///       "questionNames": [ { "id": "...", "name": "Is the disease endemic in GB?" } ],
    ///       "fieldValues": [ { "id": "...", "questionId": "...", "fieldNumber": 1, "booleanValue": true } ]
    ///     }
    /// </remarks>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The section's question names and recorded field values; empty when unanswered.</returns>
    /// <response code="200">The answers were retrieved.</response>
    [HttpGet("answers")]
    [ProducesResponseType(typeof(ProfileSectionAnswersDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ProfileSectionAnswersDto>> GetProfileSectionAnswers(
        [FromQuery] Guid profileVersionId,
        [FromQuery] Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfileSectionAnswersQuery(profileVersionId, profileSectionId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets one profile version's per-section validation status.
    /// </summary>
    /// <remarks>
    /// A section is valid once every mandatory field across its questions has a recorded answer,
    /// matching the left-hand navigation's tick/cross indicator on the legacy
    /// <c>EditProfileQuestions.aspx</c> page.
    /// </remarks>
    /// <param name="profileVersionId">The profile version to check.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Each section's validation status.</returns>
    /// <response code="200">The validation status was retrieved.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpGet("validation-status")]
    [ProducesResponseType(typeof(ProfileVersionValidationStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProfileVersionValidationStatusDto>> GetProfileVersionValidationStatus(
        [FromQuery] Guid profileVersionId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfileVersionValidationStatusQuery(profileVersionId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Validates one profile version's section against its mandatory fields.
    /// </summary>
    /// <remarks>
    /// Matches the legacy <c>ProfileVersionSection.Validate()</c> check shown by the "Validate"
    /// button on <c>EditProfileQuestions.aspx</c>.
    /// </remarks>
    /// <param name="profileVersionId">The profile version to validate.</param>
    /// <param name="profileSectionId">The section to validate.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Whether the section is valid, and which mandatory questions are unanswered.</returns>
    /// <response code="200">The section was validated.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpGet("validate")]
    [ProducesResponseType(typeof(ProfileSectionValidationResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProfileSectionValidationResultDto>> ValidateProfileVersionSection(
        [FromQuery] Guid profileVersionId,
        [FromQuery] Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new ValidateProfileVersionSectionQuery(profileVersionId, profileSectionId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Clears every recorded answer in one profile version's section.
    /// </summary>
    /// <remarks>
    /// Matches the legacy <c>ProfileVersionSection.Clear()</c> + <c>Save()</c> pathway invoked by
    /// the "Clear" button on <c>EditProfileQuestions.aspx</c>.
    /// </remarks>
    /// <param name="command">The profile version and section to clear.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The number of fields cleared.</returns>
    /// <response code="200">The section was cleared.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpPost("clear")]
    [ProducesResponseType(typeof(ClearProfileSectionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClearProfileSectionResultDto>> ClearProfileVersionSection(
        [FromBody] ClearProfileVersionSectionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Saves every submitted field answer in one profile version's section.
    /// </summary>
    /// <remarks>
    /// Matches the legacy <c>IProfileField.Save()</c> pathway invoked by the "Save" button on
    /// <c>EditProfileQuestions.aspx</c>, for standard (non-per-species, non-repeating) questions
    /// only. Answers for a per-species question, a repeating question, or an unrecognised field
    /// are silently ignored.
    /// </remarks>
    /// <param name="command">The profile version, section and submitted field answers.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The number of fields saved.</returns>
    /// <response code="200">The section was saved.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpPost("save")]
    [ProducesResponseType(typeof(SaveProfileSectionResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SaveProfileSectionResultDto>> SaveProfileVersionSection(
        [FromBody] SaveProfileVersionSectionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Saves the "All Revision dates" tab of one profile version.
    /// </summary>
    /// <remarks>
    /// Matches the legacy <c>EditProfileAllRevisionDates.aspx</c> Save button. Only sections whose
    /// submitted values differ from the stored ones are written, all in one transaction.
    /// </remarks>
    /// <param name="command">The profile version and each section's submitted revision data.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The number of sections written.</returns>
    /// <response code="200">The revision dates were saved.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="409">A section was edited by another user, or the version is not a current draft.</response>
    [HttpPut("all-revision-dates")]
    [ProducesResponseType(typeof(SaveProfileRevisionDatesResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SaveProfileRevisionDatesResultDto>> SaveProfileVersionRevisionDates(
        [FromBody] SaveProfileVersionRevisionDatesCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets every logged contribution to one profile version's section.
    /// </summary>
    /// <remarks>
    /// Matches the legacy "Contributions" tab, sourced from <c>spgProfileVersionSectionUser</c>.
    /// </remarks>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every logged contribution, most recent first.</returns>
    /// <response code="200">The contributions were retrieved.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpGet("contributions")]
    [ProducesResponseType(typeof(IReadOnlyList<ProfileSectionContributionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ProfileSectionContributionDto>>> GetProfileVersionSectionContributions(
        [FromQuery] Guid profileVersionId,
        [FromQuery] Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfileVersionSectionContributionsQuery(profileVersionId, profileSectionId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets every review comment (and reply) for one profile version's section.
    /// </summary>
    /// <remarks>
    /// Matches the legacy "Review comments" tab, sourced from the recursive
    /// <c>spgReviewCommentByProfileVersionSection</c> query.
    /// </remarks>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every review comment, in thread order.</returns>
    /// <response code="200">The review comments were retrieved.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpGet("review-comments")]
    [ProducesResponseType(typeof(IReadOnlyList<ProfileReviewCommentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ProfileReviewCommentDto>>> GetProfileSectionReviewComments(
        [FromQuery] Guid profileVersionId,
        [FromQuery] Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfileSectionReviewCommentsQuery(profileVersionId, profileSectionId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Adds a review comment, or a reply to an existing one.
    /// </summary>
    /// <remarks>
    /// Matches the legacy "Add review comment" and "Reply" panels. Allowed only while the version is
    /// an editable current draft. Supply <c>parentId</c> to reply.
    /// </remarks>
    /// <param name="command">The comment to add.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The new comment's id and row version.</returns>
    /// <response code="200">The comment was added.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="404">The comment being replied to does not exist.</response>
    [HttpPost("review-comments")]
    [ProducesResponseType(typeof(SaveProfileReviewCommentResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SaveProfileReviewCommentResultDto>> AddProfileReviewComment(
        [FromBody] AddProfileReviewCommentCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Edits a review comment.
    /// </summary>
    /// <remarks>
    /// Matches the legacy "Edit review comment" panel. Only the author may edit, and only while the
    /// comment has no replies.
    /// </remarks>
    /// <param name="command">The new values and the row version last read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The comment's new row version.</returns>
    /// <response code="200">The comment was saved.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="403">The comment cannot be edited by the acting user.</response>
    /// <response code="404">The comment does not exist.</response>
    /// <response code="409">The comment was edited by another user.</response>
    [HttpPut("review-comments")]
    [ProducesResponseType(typeof(SaveProfileReviewCommentResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<SaveProfileReviewCommentResultDto>> UpdateProfileReviewComment(
        [FromBody] UpdateProfileReviewCommentCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Deletes a review comment.
    /// </summary>
    /// <remarks>
    /// Matches the legacy "Delete" button of the review comment panel. Only the author may delete,
    /// and only while the comment has no replies.
    /// </remarks>
    /// <param name="id">The comment to delete.</param>
    /// <param name="profileVersionId">The profile version the comment belongs to.</param>
    /// <param name="profileSectionId">The section the comment belongs to.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>No content on success.</returns>
    /// <response code="204">The comment was deleted.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="403">The comment cannot be deleted by the acting user.</response>
    /// <response code="404">The comment does not exist.</response>
    [HttpDelete("review-comments/{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult> DeleteProfileReviewComment(
        [FromRoute] Guid id,
        [FromQuery] Guid profileVersionId,
        [FromQuery] Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new DeleteProfileReviewCommentCommand(id, profileVersionId, profileSectionId), cancellationToken);

        return result.ToNoContentActionResult(this);
    }

    /// <summary>
    /// Gets every section's technical/policy review dates for one profile version.
    /// </summary>
    /// <remarks>
    /// Matches the legacy "All Revision dates" tab, sourced from the revision result set of
    /// <c>spgProfileVersionSection</c>.
    /// </remarks>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Each section's revision dates, in section-number order.</returns>
    /// <response code="200">The revision dates were retrieved.</response>
    /// <response code="400">The request failed validation.</response>
    [HttpGet("all-revision-dates")]
    [ProducesResponseType(typeof(ProfileVersionAllRevisionDatesDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProfileVersionAllRevisionDatesDto>> GetProfileVersionAllRevisionDates(
        [FromQuery] Guid profileVersionId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetProfileVersionAllRevisionDatesQuery(profileVersionId), cancellationToken);

        return result.ToActionResult(this);
    }
}
