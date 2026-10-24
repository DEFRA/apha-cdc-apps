using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileQuestions.Commands;

namespace CDC.Api.Features.ProfileQuestions.Interfaces;

/// <summary>
/// Data access for profile questions. Every member maps onto the stored procedures the legacy
/// <c>Profiles.DataAccess.Sql.ProfileQuestionService</c> used, so behaviour is preserved.
/// </summary>
public interface IProfileQuestionRepository
{
    /// <summary>Reads one question via <c>spgProfileQuestion</c>.</summary>
    /// <param name="id">The question to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The question, or <see langword="null"/> when it does not exist.</returns>
    Task<ProfileQuestion?> GetProfileQuestionAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Reads the questions within one section via <c>spgProfileQuestionBySectionId</c>.</summary>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The section's questions; empty when it has none.</returns>
    Task<IReadOnlyList<ProfileQuestionInfo>> GetProfileQuestionInfoListAsync(Guid profileSectionId, CancellationToken cancellationToken);

    /// <summary>Reads several questions, in the order given, through one connection via <c>spgProfileQuestion</c>.</summary>
    /// <param name="ids">The questions to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The questions that exist; an identifier with no question is skipped.</returns>
    Task<IReadOnlyList<ProfileQuestion>> GetProfileQuestionsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>
    /// Reads the prioritisation categories and criteria behind the summary prioritisation guidance report via
    /// <c>spgaPrioritisationGuidanceData</c>. A criterion that appears more than once (a combined question such as
    /// <c>PMR01</c> is mapped twice) is listed once, under the first category it was read for.
    /// </summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The categories in display order, each with its criteria in criterion-code order.</returns>
    Task<IReadOnlyList<PrioritisationGuidanceCategory>> GetPrioritisationGuidanceAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Updates a question's guidance text and display names via <c>spuProfileQuestion</c>, then
    /// re-reads the persisted row so the result reflects every field, including the ones the
    /// stored procedure does not accept.
    /// </summary>
    /// <param name="command">The changes to apply.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The updated question, or <see langword="null"/> when it does not exist.</returns>
    /// <exception cref="Domain.Exceptions.ConcurrencyException">
    /// Thrown when <see cref="UpdateProfileQuestionCommand.LastUpdated"/> no longer matches the
    /// stored row version.
    /// </exception>
    Task<ProfileQuestion?> UpdateProfileQuestionAsync(UpdateProfileQuestionCommand command, CancellationToken cancellationToken);
}
