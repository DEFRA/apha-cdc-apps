using CDC.Common.Contracts;

namespace CDC.Api.Features.ProfileSections.Interfaces;

/// <summary>
/// Application service for the profile sections feature. Owns the mapping between domain
/// entities and the DTOs exposed over HTTP, so MediatR handlers stay thin.
/// </summary>
public interface IProfileSectionService
{
    /// <summary>Gets the profile questionnaire structure: sections, questions and fields.</summary>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The 16 profile reference sections, their questions and fields.</returns>
    Task<ProfileQuestionnaireMetadataDto> GetProfileQuestionnaireMetadataAsync(CancellationToken cancellationToken);

    /// <summary>Gets one profile version's recorded answers for one section.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The section's question names and recorded field values.</returns>
    Task<ProfileSectionAnswersDto> GetProfileSectionAnswersAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken);
}
