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

    /// <summary>Gets one profile version's per-section validation status: whether every
    /// mandatory field in each section has a recorded answer.</summary>
    /// <param name="profileVersionId">The profile version to check.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The profile version's per-section validation status.</returns>
    Task<ProfileVersionValidationStatusDto> GetProfileVersionValidationStatusAsync(
        Guid profileVersionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Clears every recorded answer in one section, matching the legacy
    /// <c>ProfileVersionSection.Clear()</c> + <c>Save()</c> pathway.
    /// </summary>
    /// <param name="profileVersionId">The profile version to clear.</param>
    /// <param name="profileSectionId">The section to clear.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number of fields cleared.</returns>
    Task<ClearProfileSectionResultDto> ClearProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Validates one section's recorded answers, matching the legacy
    /// <c>ProfileVersionSection.Validate()</c> mandatory-field check.
    /// </summary>
    /// <param name="profileVersionId">The profile version to validate.</param>
    /// <param name="profileSectionId">The section to validate.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Whether the section is valid, and which mandatory questions are unanswered.</returns>
    Task<ProfileSectionValidationResultDto> ValidateProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Saves every submitted field answer in one section, matching the legacy
    /// <c>IProfileField.Save()</c> pathway for standard (non-per-species, non-repeating)
    /// questions. Answers for a per-species question, a repeating question, or an unrecognised
    /// field are silently ignored, since those are not yet supported for editing.
    /// </summary>
    /// <param name="profileVersionId">The profile version being answered.</param>
    /// <param name="profileSectionId">The section being answered.</param>
    /// <param name="answers">The submitted field answers.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number of fields saved.</returns>
    Task<SaveProfileSectionResultDto> SaveProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        IReadOnlyList<ProfileFieldAnswerInputDto> answers,
        CancellationToken cancellationToken);

    /// <summary>Gets every logged contribution to one profile version's section, for the
    /// "Contributions" tab.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Every logged contribution, most recent first.</returns>
    Task<IReadOnlyList<ProfileSectionContributionDto>> GetProfileVersionSectionContributionsAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken);

    /// <summary>Gets every review comment (and reply) for one profile version's section, for the
    /// "Review comments" tab.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Every review comment, in thread order.</returns>
    Task<IReadOnlyList<ProfileReviewCommentDto>> GetProfileSectionReviewCommentsAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken);

    /// <summary>Gets every section's technical/policy review dates for one profile version, for
    /// the "All Revision dates" tab.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Each section's revision dates, in section-number order.</returns>
    Task<ProfileVersionAllRevisionDatesDto> GetProfileVersionAllRevisionDatesAsync(
        Guid profileVersionId,
        CancellationToken cancellationToken);
}
