using CDC.Api.Domain.Entities;
using CDC.Common.Contracts;

namespace CDC.Api.Features.ProfileReports.Interfaces;

/// <summary>
/// Data access for the content of the profile report PDFs: the questionnaire (with section notes
/// and relevance rules), each section's recorded answers, and the options of list fields. Reads
/// the same stored procedures the legacy report classes loaded through the CSLA business objects.
/// </summary>
public interface IProfileReportContentRepository
{
    /// <summary>Reads the questionnaire via <c>spgaProfileSectionMetadata</c>.</summary>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The sections, questions and fields.</returns>
    Task<ReportQuestionnaire> GetQuestionnaireAsync(CancellationToken cancellationToken);

    /// <summary>Reads one section's profiled species, question names, answer rows and field values via <c>spgProfileVersionSection</c>.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The recorded answers; empty collections when the section is unanswered.</returns>
    Task<ReportSectionAnswers> GetSectionAnswersAsync(Guid profileVersionId, Guid profileSectionId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads what the validation needs to know about a profile version as a whole via <c>spgProfileVersionInfoById</c>:
    /// whether it belongs to a "what-if" scenario and whether any of its species is inactive.
    /// </summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The version's context; the defaults when the version does not exist.</returns>
    Task<ReportVersionContext> GetVersionContextAsync(Guid profileVersionId, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a list field's options: a maintainable reference table via <c>spgReferenceValueByTable</c>
    /// (which knows which values are inactive), any other via <c>spgLookupValueByTable</c>.
    /// </summary>
    /// <param name="referenceTableId">The reference table to read.</param>
    /// <param name="isMaintainable">Whether the table is user-maintainable.</param>
    /// <param name="cancellationToken">Cancels the database call.</param>
    /// <returns>The options in display order.</returns>
    Task<IReadOnlyList<ReportReferenceItem>> GetReferenceItemsAsync(Guid referenceTableId, bool isMaintainable, CancellationToken cancellationToken);

    /// <summary>
    /// Reads a profile version's prioritisation scores via <c>spgPrioritisationCategoryScoreByProfileVersionId</c>,
    /// <c>spgPrioritisationCriterionScoreByProfileVersionId</c> and <c>spgPrioritisedSpeciesByProfileVersionId</c>.
    /// </summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="cancellationToken">Cancels the database calls.</param>
    /// <returns>The scores; empty lists when prioritisation has not been run for the version.</returns>
    Task<ProfilePrioritisationScoresDto> GetPrioritisationScoresAsync(Guid profileVersionId, CancellationToken cancellationToken);

    /// <summary>Reads the user guidance of each given question via <c>spgProfileQuestion</c>.</summary>
    /// <param name="questionIds">The questions whose guidance is wanted.</param>
    /// <param name="cancellationToken">Cancels the database calls.</param>
    /// <returns>The guidance of each question that exists, in the order requested.</returns>
    Task<IReadOnlyList<ProfileQuestionGuidanceDto>> GetQuestionGuidanceAsync(IReadOnlyList<Guid> questionIds, CancellationToken cancellationToken);
}
