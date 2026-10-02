using CDC.Api.Application.Extensions;
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
}
