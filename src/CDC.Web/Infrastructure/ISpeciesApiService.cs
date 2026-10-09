using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Typed client for the species endpoints on CDC.Api.
/// </summary>
public interface ISpeciesApiService
{
    /// <summary>Calls <c>GET /api/species</c> and returns every species and species group.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The full species list, in the order returned by the API.</returns>
    Task<IReadOnlyList<SpeciesDto>> GetAllSpeciesAsync(CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/species/{speciesId}/detail</c>.</summary>
    /// <param name="speciesId">The species to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The species detail, or <see langword="null"/> when the species does not exist.</returns>
    Task<SpeciesDetailDto?> GetSpeciesDetailAsync(Guid speciesId, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/species/{speciesId}/valid-parents</c>.</summary>
    /// <param name="speciesId">The species being re-parented.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The valid parent list.</returns>
    Task<IReadOnlyList<SpeciesValidParentDto>> GetSpeciesValidParentsAsync(Guid speciesId, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>PUT /api/species/name-parent</c>.</summary>
    /// <param name="request">The change to apply.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome of the call.</returns>
    Task<UpdateSpeciesNameParentResult> UpdateSpeciesNameParentAsync(
        UpdateSpeciesNameParentRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>POST /api/species</c> to add a new species or species group.</summary>
    /// <param name="request">The species to add.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome of the call, including the new species identifier on success.</returns>
    Task<AddSpeciesResult> AddSpeciesAsync(AddSpeciesRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/species/audit-trail</c>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The audit trail, most recent entry first.</returns>
    Task<IReadOnlyList<SpeciesAuditTrailEntryDto>> GetSpeciesAuditTrailAsync(CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/species/metadata</c>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every section, its questions and each question's fields.</returns>
    Task<SpeciesMetadataDto> GetSpeciesMetadataAsync(CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/species/{speciesId}/answers</c>.</summary>
    /// <param name="speciesId">The species to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The recorded answers, or <see langword="null"/> when the species does not exist.</returns>
    Task<SpeciesAnswerDataDto?> GetSpeciesAnswerDataAsync(Guid speciesId, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/reference-data/{referenceTableId}/values</c>, used to resolve
    /// "List" type question field options (for example a species questionnaire checkbox group).</summary>
    /// <param name="referenceTableId">The reference table to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The reference values; an empty list when the table has none.</returns>
    Task<IReadOnlyList<ReferenceValueDto>> GetReferenceValuesAsync(Guid referenceTableId, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>PUT /api/species/{speciesId}/position</c> to swap the species' sequence
    /// number with its previous or next sibling.</summary>
    /// <param name="speciesId">The species to move.</param>
    /// <param name="isMovingUp">True to swap with the previous sibling; false for the next.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task ChangeSpeciesPositionAsync(Guid speciesId, bool isMovingUp, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>PUT /api/species/answers</c> to save question responses for one species.</summary>
    /// <param name="request">The changes to apply.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome of the call.</returns>
    Task<UpdateSpeciesAnswerDataResult> UpdateSpeciesAnswerDataAsync(
        UpdateSpeciesAnswerDataRequestDto request,
        CancellationToken cancellationToken = default);
}
