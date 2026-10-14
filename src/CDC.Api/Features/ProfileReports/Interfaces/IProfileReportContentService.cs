using CDC.Api.Domain.Common;
using CDC.Common.Contracts;

namespace CDC.Api.Features.ProfileReports.Interfaces;

/// <summary>Builds the display-ready content the profile report PDFs print.</summary>
public interface IProfileReportContentService
{
    /// <summary>Gets a profile version's report content.</summary>
    /// <param name="profileVersionId">The profile version to report on.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The content; not found when the profile version does not exist.</returns>
    Task<Result<ProfileReportContentDto>> GetProfileReportContentAsync(Guid profileVersionId, CancellationToken cancellationToken);

    /// <summary>Gets the prioritisation scores the summary prioritisation report prints.</summary>
    /// <param name="profileVersionId">The profile version to report on.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The scores (empty when prioritisation has not been run); not found when the profile version does not exist.</returns>
    Task<Result<ProfilePrioritisationScoresDto>> GetProfilePrioritisationScoresAsync(Guid profileVersionId, CancellationToken cancellationToken);

    /// <summary>Gets the user guidance the bespoke report prints under its guidance headings.</summary>
    /// <param name="questionIds">The questions whose guidance is wanted.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The guidance of each question that exists.</returns>
    Task<Result<IReadOnlyList<ProfileQuestionGuidanceDto>>> GetQuestionGuidanceAsync(IReadOnlyList<Guid> questionIds, CancellationToken cancellationToken);
}
