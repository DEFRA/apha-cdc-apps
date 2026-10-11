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

    /// <summary>
    /// Clears every recorded field value matched by <paramref name="instructions"/>, then logs
    /// the contribution, matching the legacy <c>ProfileVersionSection.Clear()</c> + <c>Save()</c>
    /// pathway.
    /// </summary>
    /// <param name="profileVersionId">The profile version to clear.</param>
    /// <param name="profileSectionId">The section being cleared, for the contribution log.</param>
    /// <param name="instructions">The exact rows to clear.</param>
    /// <param name="userId">The acting user, for the contribution log.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The number of fields cleared.</returns>
    Task<int> ClearProfileVersionSectionAnswersAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        IReadOnlyList<ProfileFieldClearInstruction> instructions,
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Writes every recorded field value in <paramref name="instructions"/>, then logs the
    /// contribution, matching the legacy <c>IProfileField.Save()</c> pathway for standard
    /// (non-per-species, non-repeating) questions.
    /// </summary>
    /// <param name="profileVersionId">The profile version being answered.</param>
    /// <param name="profileSectionId">The section being answered, for the contribution log.</param>
    /// <param name="instructions">The exact field values to write.</param>
    /// <param name="userId">The acting user, for the contribution log.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The number of fields saved.</returns>
    Task<int> SaveProfileVersionSectionAnswersAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        IReadOnlyList<ProfileFieldSaveInstruction> instructions,
        Guid userId,
        CancellationToken cancellationToken);

    /// <summary>Reads every logged contribution to one profile version's section via
    /// <c>spgProfileVersionSectionUser</c>, for the "Contributions" tab.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>Every logged contribution, most recent first.</returns>
    Task<IReadOnlyList<ProfileSectionContribution>> GetProfileVersionSectionContributionsAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken);

    /// <summary>Reads every review comment (and reply) for one profile version's section via
    /// <c>spgReviewCommentByProfileVersionSection</c>, for the "Review comments" tab.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>Every review comment, in thread order (each reply immediately follows its parent).</returns>
    Task<IReadOnlyList<ProfileReviewComment>> GetProfileSectionReviewCommentsAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken);

    /// <summary>Reads one section's technical/policy review dates via <c>spgProfileVersionSection</c>,
    /// for the "All Revision dates" tab.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The section's revision dates, or <see langword="null"/> when none are recorded.</returns>
    Task<ProfileSectionRevisionInfo?> GetProfileVersionSectionRevisionInfoAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken);
}
