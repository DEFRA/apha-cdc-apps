using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>Typed client for the profile question endpoints on CDC.Api.</summary>
public interface IProfileQuestionsApiService
{
    /// <summary>Calls <c>GET /api/profile-questions/{profileQuestionId}</c>.</summary>
    /// <param name="profileQuestionId">The question to read.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The question, or <see langword="null"/> when it does not exist.</returns>
    Task<ProfileQuestionEditDto?> GetProfileQuestionAsync(Guid profileQuestionId, CancellationToken cancellationToken = default);

    /// <summary>Calls <c>GET /api/profile-questions/guidance-content</c>.</summary>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>Every section's questions with their guidance, and the prioritisation criteria; or a forbidden outcome.</returns>
    Task<ProfileGuidanceContentResult> GetProfileGuidanceContentAsync(CancellationToken cancellationToken = default);

    /// <summary>Calls <c>PUT /api/profile-questions/{profileQuestionId}</c>.</summary>
    /// <param name="profileQuestionId">The question to change.</param>
    /// <param name="request">The changes to apply.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The outcome of the call.</returns>
    Task<ProfileQuestionUpdateResult> UpdateProfileQuestionAsync(
        Guid profileQuestionId,
        UpdateProfileQuestionRequest request,
        CancellationToken cancellationToken = default);
}
