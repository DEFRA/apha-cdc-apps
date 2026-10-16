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

    /// <summary>Gets one profile version's per-section validation status: whether the legacy
    /// <c>ProfileVersionSection.Validate()</c> finds no errors in each section, and which of its
    /// questions have errors.</summary>
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
    /// <c>ProfileVersionSection.Validate()</c>: the revision-date rules, then every question's
    /// mandatory, inactive reference data, relevance, species and range rules.
    /// </summary>
    /// <param name="profileVersionId">The profile version to validate.</param>
    /// <param name="profileSectionId">The section to validate.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Whether the section is valid, and every validation error found.</returns>
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

    /// <summary>Gets every section's author/VA/policy review data for one profile version, plus the
    /// frequency and status options, for the "All Revision dates" tab.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Each section's revision data (in section-number order) and the drop-down options.</returns>
    Task<ProfileVersionAllRevisionDatesDto> GetProfileVersionAllRevisionDatesAsync(
        Guid profileVersionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Saves the "All Revision dates" tab, matching the legacy <c>ProfileVersionSection.Save()</c>
    /// pathway for the revision fields. Only sections whose submitted values differ from the stored
    /// ones are written. A review's completed date is set to now when its status changes to
    /// "Complete" and cleared when it changes to anything else.
    /// </summary>
    /// <param name="profileVersionId">The profile version being edited.</param>
    /// <param name="sections">The submitted revision data of each section.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The number of sections written.</returns>
    /// <exception cref="FluentValidation.ValidationException">
    /// Thrown when a submitted section, frequency or status is not recognised.
    /// </exception>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">
    /// Thrown when a section was edited by another user, or the version is not a current draft.
    /// </exception>
    Task<SaveProfileRevisionDatesResultDto> SaveProfileVersionRevisionDatesAsync(
        Guid profileVersionId,
        IReadOnlyList<ProfileSectionRevisionDatesInputDto> sections,
        CancellationToken cancellationToken);
}
