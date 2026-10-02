using CDC.Common.Contracts;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Typed client for the profile sections endpoints on CDC.Api.
/// </summary>
public interface IProfileSectionsApiService
{
    /// <summary>Calls <c>GET /api/profile-sections/metadata</c>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The 16 profile reference sections, their questions and fields.</returns>
    Task<ProfileQuestionnaireMetadataDto> GetProfileQuestionnaireMetadataAsync(CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/profile-sections/answers</c>.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The section's question names and recorded field values.</returns>
    Task<ProfileSectionAnswersDto> GetProfileSectionAnswersAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/reference-data/{referenceTableId}/values</c>, used to resolve
    /// "List"/"MultiValueList" type question field options.</summary>
    /// <param name="referenceTableId">The reference table to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The reference values; an empty list when the table has none.</returns>
    Task<IReadOnlyList<ReferenceValueDto>> GetReferenceValuesAsync(Guid referenceTableId, CancellationToken cancellationToken = default);
}
