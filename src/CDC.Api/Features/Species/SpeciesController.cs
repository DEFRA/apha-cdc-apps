using CDC.Api.Application.Extensions;
using CDC.Api.Features.Species.Commands;
using CDC.Api.Features.Species.Dtos;
using CDC.Api.Features.Species.Queries;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace CDC.Api.Features.Species;

/// <summary>
/// Species reference data, questionnaire metadata and answer data. Replaces the legacy
/// <c>ISpeciesDataService</c> WCF endpoint.
/// </summary>
/// <param name="mediator">Dispatches queries and commands to their handlers.</param>
[ApiController]
[Route("api/species")]
[Produces("application/json")]
[ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
public sealed class SpeciesController(ISender mediator) : ControllerBase
{
    /// <summary>
    /// Gets every species and species group.
    /// </summary>
    /// <remarks>
    /// The hierarchy is returned flat; use <c>parentId</c> to rebuild the tree. A root entry
    /// has a <c>parentId</c> of <c>00000000-0000-0000-0000-000000000000</c>.
    ///
    /// Sample response:
    ///
    ///     [
    ///       {
    ///         "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
    ///         "parentId": "00000000-0000-0000-0000-000000000000",
    ///         "description": "Cattle",
    ///         "isActive": true,
    ///         "isInUse": true
    ///       }
    ///     ]
    /// </remarks>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The full species list, in sequence order.</returns>
    /// <response code="200">The species list was retrieved.</response>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SpeciesDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SpeciesDto>>> GetAllSpecies(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAllSpeciesQuery(), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets the species questionnaire structure.
    /// </summary>
    /// <remarks>
    /// Returns every section, its questions, and each question's fields. The structure is the
    /// same for all species; answers are retrieved separately.
    ///
    /// Sample response:
    ///
    ///     {
    ///       "sections": [
    ///         {
    ///           "id": "1f2e3d4c-5b6a-7988-9a0b-1c2d3e4f5a6b",
    ///           "name": "Epidemiology",
    ///           "shortName": "Epi",
    ///           "sectionNumber": 1,
    ///           "questions": [ { "id": "...", "fields": [ { "id": "...", "dataTypeName": "Boolean" } ] } ]
    ///         }
    ///       ]
    ///     }
    /// </remarks>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Sections, questions and fields.</returns>
    /// <response code="200">The metadata was retrieved.</response>
    [HttpGet("metadata")]
    [ProducesResponseType(typeof(SpeciesMetadataDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SpeciesMetadataDto>> GetSpeciesMetadata(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSpeciesMetadataQuery(), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets the recorded answers for one species.
    /// </summary>
    /// <remarks>
    /// Answers are grouped by questionnaire section. Keep the returned <c>lastUpdated</c> row
    /// version and send it back when updating, so a concurrent edit can be detected.
    ///
    /// Sample response:
    ///
    ///     {
    ///       "speciesId": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
    ///       "speciesName": "Cattle",
    ///       "lastUpdated": "AAAAAAAAB9E=",
    ///       "sections": [
    ///         {
    ///           "sectionId": "1f2e3d4c-5b6a-7988-9a0b-1c2d3e4f5a6b",
    ///           "fieldValues": [ { "id": "...", "questionId": "...", "fieldNumber": 1, "booleanValue": true } ]
    ///         }
    ///       ]
    ///     }
    /// </remarks>
    /// <param name="speciesId">The species to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The recorded answers.</returns>
    /// <response code="200">The answer data was retrieved.</response>
    /// <response code="404">No species exists with the supplied identifier.</response>
    [HttpGet("{speciesId:guid}/answers")]
    [ProducesResponseType(typeof(SpeciesAnswerDataDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SpeciesAnswerDataDto>> GetSpeciesAnswerData(
        Guid speciesId,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetSpeciesAnswerDataQuery(speciesId), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Gets the species selected against a named disease filter.
    /// </summary>
    /// <remarks>
    /// Mirrors the legacy <c>GetAllSelectedSpecies</c> operation, which filters on the disease
    /// name rather than a profile version.
    ///
    /// Sample request:
    ///
    ///     GET /api/species/selected?diseaseName=Bovine%20tuberculosis
    /// </remarks>
    /// <param name="diseaseName">The disease name to filter by.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The selected species; an empty list when the disease name is unknown.</returns>
    /// <response code="200">The selected species were retrieved.</response>
    /// <response code="400">No disease name was supplied.</response>
    [HttpGet("selected")]
    [ProducesResponseType(typeof(IReadOnlyList<SelectedSpeciesDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<SelectedSpeciesDto>>> GetAllSelectedSpecies(
        [FromQuery] string diseaseName,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAllSelectedSpeciesQuery(diseaseName), cancellationToken);

        return result.ToActionResult(this);
    }

    /// <summary>
    /// Updates the recorded answers for one species.
    /// </summary>
    /// <remarks>
    /// All changes are applied in a single transaction, followed by a recalculation of the
    /// species prioritisation scores. Supply the <c>lastUpdated</c> row version returned by
    /// <c>GET /api/species/{speciesId}/answers</c>; if another user has saved in the meantime
    /// the request is rejected with 409 and nothing is written.
    ///
    /// <c>kind</c> selects which value is stored: <c>0</c> clears the answer, <c>1</c> boolean,
    /// <c>2</c> single list value, <c>3</c> text, <c>4</c> multi-select list. A multi-value
    /// change replaces every stored value for that field.
    ///
    /// Sample request:
    ///
    ///     PUT /api/species/answers
    ///     {
    ///       "speciesId": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
    ///       "lastUpdated": "AAAAAAAAB9E=",
    ///       "changes": [
    ///         { "fieldId": "0c1d2e3f-4a5b-6c7d-8e9f-0a1b2c3d4e5f", "kind": 1, "booleanValue": true },
    ///         { "fieldId": "1d2e3f4a-5b6c-7d8e-9f0a-1b2c3d4e5f60", "kind": 3, "textValue": "Endemic in GB" },
    ///         { "fieldId": "2e3f4a5b-6c7d-8e9f-0a1b-2c3d4e5f6071", "kind": 4, "multiValues": [ "3f4a5b6c-7d8e-9f0a-1b2c-3d4e5f607182" ] }
    ///       ]
    ///     }
    ///
    /// Sample response:
    ///
    ///     {
    ///       "speciesId": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
    ///       "lastUpdated": "AAAAAAAAB9I="
    ///     }
    /// </remarks>
    /// <param name="command">The changes to apply.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The species identifier and its new row version.</returns>
    /// <response code="200">The changes were committed.</response>
    /// <response code="400">The request failed validation.</response>
    /// <response code="409">Another user has saved this species since it was read.</response>
    [HttpPut("answers")]
    [ProducesResponseType(typeof(UpdateSpeciesAnswerDataResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UpdateSpeciesAnswerDataResultDto>> UpdateSpeciesAnswerData(
        [FromBody] UpdateSpeciesAnswerDataCommand command,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(command, cancellationToken);

        return result.ToActionResult(this);
    }
}
