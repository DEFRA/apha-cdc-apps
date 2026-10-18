using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

// Test double for IProfilePropertiesApiService so page-model and integration tests don't need a real HTTP call.
internal sealed class FakeProfilePropertiesApiService(
    NewProfileDefaultsDto? defaults = null,
    Exception? throwOnGetDefaults = null,
    CreateProfileResult? createResult = null,
    UpdateProfileAttributesResult? updateResult = null,
    Exception? throwOnSave = null) : IProfilePropertiesApiService
{
    /// <summary>Gets the clone source and scenario flag the page last asked for defaults with.</summary>
    public (Guid CloneProfileVersionId, bool IsWhatIfScenario)? LastDefaultsRequest { get; private set; }

    /// <summary>Gets the last profile the page asked to create.</summary>
    public CreateProfileRequest? LastCreateRequest { get; private set; }

    /// <summary>Gets the last profile the page asked to update, and how.</summary>
    public (Guid ProfileId, UpdateProfileAttributesRequest Request)? LastUpdate { get; private set; }

    public Task<NewProfileDefaultsDto?> GetNewProfileDefaultsAsync(
        Guid cloneProfileVersionId,
        bool isWhatIfScenario,
        CancellationToken cancellationToken = default)
    {
        LastDefaultsRequest = (cloneProfileVersionId, isWhatIfScenario);

        return throwOnGetDefaults is not null
            ? Task.FromException<NewProfileDefaultsDto?>(throwOnGetDefaults)
            : Task.FromResult(defaults);
    }

    public Task<CreateProfileResult> CreateProfileAsync(CreateProfileRequest request, CancellationToken cancellationToken = default)
    {
        LastCreateRequest = request;

        return throwOnSave is not null
            ? Task.FromException<CreateProfileResult>(throwOnSave)
            : Task.FromResult(createResult ?? new CreateProfileResult(SaveProfileOutcome.Success, request.Id, null));
    }

    public Task<UpdateProfileAttributesResult> UpdateProfileAttributesAsync(
        Guid profileId,
        UpdateProfileAttributesRequest request,
        CancellationToken cancellationToken = default)
    {
        LastUpdate = (profileId, request);

        return throwOnSave is not null
            ? Task.FromException<UpdateProfileAttributesResult>(throwOnSave)
            : Task.FromResult(updateResult ?? new UpdateProfileAttributesResult(SaveProfileOutcome.Success, null));
    }
}
