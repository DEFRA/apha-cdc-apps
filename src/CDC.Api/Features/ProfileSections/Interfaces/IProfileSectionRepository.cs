using CDC.Api.Domain.Entities;

namespace CDC.Api.Features.ProfileSections.Interfaces;

/// <summary>
/// Data access for the profile reference sections, their questionnaire metadata, and the
/// recorded answers for a profile version. Implementations must contain no business logic.
/// </summary>
public interface IProfileSectionRepository
{
    /// <summary>Reads the questionnaire structure via <c>spgaProfileSectionMetadata</c>.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The 16 profile reference sections, their questions and fields.</returns>
    Task<ProfileQuestionnaireMetadata> GetProfileQuestionnaireMetadataAsync(CancellationToken cancellationToken);

    /// <summary>Reads one profile version's recorded answers for one section via <c>spgProfileVersionSection</c>.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The section's question names and recorded field values.</returns>
    Task<ProfileSectionAnswers> GetProfileSectionAnswersAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken);
}
