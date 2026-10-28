using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

// Test double for IProfileQuestionsApiService so page-model tests don't need a real HTTP call.
internal sealed class FakeProfileQuestionsApiService(
    ProfileQuestionEditDto? question = null,
    ProfileQuestionUpdateResult? updateResult = null,
    Exception? throwOnGet = null,
    Exception? throwOnUpdate = null,
    ProfileGuidanceContentResult? guidanceContent = null,
    Exception? throwOnGetGuidanceContent = null) : IProfileQuestionsApiService
{
    /// <summary>Gets the number of times <c>GetProfileGuidanceContentAsync</c> was called.</summary>
    public int GuidanceContentCallCount { get; private set; }

    public Task<ProfileGuidanceContentResult> GetProfileGuidanceContentAsync(CancellationToken cancellationToken = default)
    {
        GuidanceContentCallCount++;

        return throwOnGetGuidanceContent is not null
            ? Task.FromException<ProfileGuidanceContentResult>(throwOnGetGuidanceContent)
            : Task.FromResult(guidanceContent ?? new ProfileGuidanceContentResult(ProfileGuidanceContentOutcome.Success, new ProfileGuidanceContentDto()));
    }

    /// <summary>Gets the question most recently passed to <c>UpdateProfileQuestionAsync</c>.</summary>
    public Guid? UpdatedQuestionId { get; private set; }

    /// <summary>Gets the request most recently passed to <c>UpdateProfileQuestionAsync</c>.</summary>
    public UpdateProfileQuestionRequest? UpdateRequest { get; private set; }

    /// <summary>Gets the number of times <c>UpdateProfileQuestionAsync</c> was called.</summary>
    public int UpdateCallCount { get; private set; }

    public Task<ProfileQuestionEditDto?> GetProfileQuestionAsync(Guid profileQuestionId, CancellationToken cancellationToken = default) =>
        throwOnGet is not null
            ? Task.FromException<ProfileQuestionEditDto?>(throwOnGet)
            : Task.FromResult(question);

    public Task<ProfileQuestionUpdateResult> UpdateProfileQuestionAsync(
        Guid profileQuestionId,
        UpdateProfileQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        UpdateCallCount++;
        UpdatedQuestionId = profileQuestionId;
        UpdateRequest = request;

        return throwOnUpdate is not null
            ? Task.FromException<ProfileQuestionUpdateResult>(throwOnUpdate)
            : Task.FromResult(updateResult ?? new ProfileQuestionUpdateResult { Outcome = ProfileQuestionUpdateOutcome.Success });
    }
}
