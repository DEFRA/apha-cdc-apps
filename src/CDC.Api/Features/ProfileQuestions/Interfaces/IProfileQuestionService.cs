using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileQuestions.Commands;
using CDC.Api.Features.ProfileQuestions.Dtos;

namespace CDC.Api.Features.ProfileQuestions.Interfaces;

/// <summary>
/// Application service for the profile questions feature. Owns the mapping between domain
/// entities and the DTOs exposed over HTTP, so MediatR handlers stay thin.
/// </summary>
public interface IProfileQuestionService
{
    /// <summary>Gets one profile question.</summary>
    /// <param name="id">The question to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The question, or <see langword="null"/> when it does not exist.</returns>
    Task<ProfileQuestionDto?> GetProfileQuestionAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Gets the questions within one profile section.</summary>
    /// <param name="profileSectionId">The section to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The section's questions; empty when it has none.</returns>
    Task<IReadOnlyList<ProfileQuestionInfoDto>> GetProfileQuestionInfoListAsync(Guid profileSectionId, CancellationToken cancellationToken);

    /// <summary>Gets a profile guidance report descriptor.</summary>
    /// <param name="reportType">The report to retrieve.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report descriptor.</returns>
    Task<ProfileGuidanceReportDto> GetProfileGuidanceReportAsync(ProfileGuidanceReportType reportType, CancellationToken cancellationToken);

    /// <summary>Updates a question's guidance text and display names.</summary>
    /// <param name="command">The changes to apply.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The updated question, or <see langword="null"/> when it does not exist.</returns>
    Task<ProfileQuestionDto?> UpdateProfileQuestionAsync(UpdateProfileQuestionCommand command, CancellationToken cancellationToken);
}
