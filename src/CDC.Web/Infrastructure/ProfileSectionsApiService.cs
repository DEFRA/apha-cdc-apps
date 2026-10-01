using System.Net.Http.Json;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Default <see cref="IProfileSectionsApiService"/>. The base address is configured on the
/// underlying <see cref="HttpClient"/> in Program.cs (from <c>Api:BaseUrl</c>), so no URL is
/// hardcoded here. Failures are left to propagate - the caller (<c>EditProfileQuestionsModel</c>)
/// decides how to present them, matching the pattern used by <see cref="SpeciesApiService"/>.
/// </summary>
/// <param name="httpClient">Typed client pointing at CDC.Api.</param>
public sealed class ProfileSectionsApiService(HttpClient httpClient) : IProfileSectionsApiService
{
    /// <inheritdoc />
    public async Task<ProfileQuestionnaireMetadataDto> GetProfileQuestionnaireMetadataAsync(CancellationToken cancellationToken = default)
    {
        var metadata = await httpClient.GetFromJsonAsync<ProfileQuestionnaireMetadataDto>(
            "/api/profile-sections/metadata", cancellationToken);

        return metadata ?? new ProfileQuestionnaireMetadataDto();
    }

    /// <inheritdoc />
    public async Task<ProfileSectionAnswersDto> GetProfileSectionAnswersAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default)
    {
        var answers = await httpClient.GetFromJsonAsync<ProfileSectionAnswersDto>(
            $"/api/profile-sections/answers?profileVersionId={profileVersionId}&profileSectionId={profileSectionId}",
            cancellationToken);

        return answers ?? new ProfileSectionAnswersDto();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ReferenceValueDto>> GetReferenceValuesAsync(Guid referenceTableId, CancellationToken cancellationToken = default)
    {
        var values = await httpClient.GetFromJsonAsync<IReadOnlyList<ReferenceValueDto>>(
            $"/api/reference-data/{referenceTableId}/values", cancellationToken);

        return values ?? [];
    }
}
