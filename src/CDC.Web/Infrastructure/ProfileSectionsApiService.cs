using System.Net;
using System.Net.Http.Json;
using CDC.Common.Contracts;
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

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileNoteTypeDto>> GetProfileNoteTypesAsync(CancellationToken cancellationToken = default)
    {
        var noteTypes = await httpClient.GetFromJsonAsync<IReadOnlyList<ProfileNoteTypeDto>>(
            "/api/profile-notes/types", cancellationToken);

        return noteTypes ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileNoteDto>> GetProfileNotesBySectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid noteTypeId,
        CancellationToken cancellationToken = default)
    {
        var notes = await httpClient.GetFromJsonAsync<IReadOnlyList<ProfileNoteDto>>(
            $"/api/profile-notes/by-section?profileVersionId={profileVersionId}&profileSectionId={profileSectionId}&noteTypeId={noteTypeId}",
            cancellationToken);

        return notes ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileNoteDto>> GetProfileNotesByVersionAsync(
        Guid profileVersionId,
        Guid noteTypeId,
        CancellationToken cancellationToken = default)
    {
        var notes = await httpClient.GetFromJsonAsync<IReadOnlyList<ProfileNoteDto>>(
            $"/api/profile-notes/by-version?profileVersionId={profileVersionId}&noteTypeId={noteTypeId}",
            cancellationToken);

        return notes ?? [];
    }

    /// <inheritdoc />
    public async Task<ProfileVersionValidationStatusDto> GetProfileVersionValidationStatusAsync(
        Guid profileVersionId,
        CancellationToken cancellationToken = default)
    {
        var status = await httpClient.GetFromJsonAsync<ProfileVersionValidationStatusDto>(
            $"/api/profile-sections/validation-status?profileVersionId={profileVersionId}",
            cancellationToken);

        return status ?? new ProfileVersionValidationStatusDto { ProfileVersionId = profileVersionId };
    }

    /// <inheritdoc />
    public async Task<ProfileSectionValidationResultDto> ValidateProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<ProfileSectionValidationResultDto>(
            $"/api/profile-sections/validate?profileVersionId={profileVersionId}&profileSectionId={profileSectionId}",
            cancellationToken);

        return result ?? new ProfileSectionValidationResultDto { IsValid = true };
    }

    /// <inheritdoc />
    public async Task<ClearProfileSectionResultDto> ClearProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default)
    {
        var request = new { ProfileVersionId = profileVersionId, ProfileSectionId = profileSectionId };
        var response = await httpClient.PostAsJsonAsync("/api/profile-sections/clear", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<ClearProfileSectionResultDto>(cancellationToken);

        return result ?? new ClearProfileSectionResultDto();
    }

    /// <inheritdoc />
    public async Task<SaveProfileSectionResultDto> SaveProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        IReadOnlyList<ProfileFieldAnswerInputDto> answers,
        CancellationToken cancellationToken = default)
    {
        var request = new { ProfileVersionId = profileVersionId, ProfileSectionId = profileSectionId, Answers = answers };
        var response = await httpClient.PostAsJsonAsync("/api/profile-sections/save", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SaveProfileSectionResultDto>(cancellationToken);

        return result ?? new SaveProfileSectionResultDto();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileSectionContributionDto>> GetProfileVersionSectionContributionsAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default)
    {
        var contributions = await httpClient.GetFromJsonAsync<IReadOnlyList<ProfileSectionContributionDto>>(
            $"/api/profile-sections/contributions?profileVersionId={profileVersionId}&profileSectionId={profileSectionId}",
            cancellationToken);

        return contributions ?? [];
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileReviewCommentDto>> GetProfileSectionReviewCommentsAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken = default)
    {
        var comments = await httpClient.GetFromJsonAsync<IReadOnlyList<ProfileReviewCommentDto>>(
            $"/api/profile-sections/review-comments?profileVersionId={profileVersionId}&profileSectionId={profileSectionId}",
            cancellationToken);

        return comments ?? [];
    }

    /// <inheritdoc />
    public async Task<ReviewCommentResult> AddProfileReviewCommentAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid? parentId,
        string subject,
        string commentReference,
        string commentText,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            ProfileVersionId = profileVersionId,
            ProfileSectionId = profileSectionId,
            ParentId = parentId,
            Subject = subject,
            CommentReference = commentReference,
            CommentText = commentText
        };
        using var response = await httpClient.PostAsJsonAsync("/api/profile-sections/review-comments", request, cancellationToken);

        return ToReviewCommentResult(response.StatusCode);
    }

    /// <inheritdoc />
    public async Task<ReviewCommentResult> UpdateProfileReviewCommentAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid commentId,
        string subject,
        string commentReference,
        string commentText,
        byte[] lastUpdated,
        CancellationToken cancellationToken = default)
    {
        var request = new
        {
            Id = commentId,
            ProfileVersionId = profileVersionId,
            ProfileSectionId = profileSectionId,
            Subject = subject,
            CommentReference = commentReference,
            CommentText = commentText,
            LastUpdated = lastUpdated
        };
        using var response = await httpClient.PutAsJsonAsync("/api/profile-sections/review-comments", request, cancellationToken);

        return ToReviewCommentResult(response.StatusCode);
    }

    /// <inheritdoc />
    public async Task<ReviewCommentResult> DeleteProfileReviewCommentAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        Guid commentId,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.DeleteAsync(
            $"/api/profile-sections/review-comments/{commentId}?profileVersionId={profileVersionId}&profileSectionId={profileSectionId}",
            cancellationToken);

        return ToReviewCommentResult(response.StatusCode);
    }

    private static ReviewCommentResult ToReviewCommentResult(HttpStatusCode statusCode) => statusCode switch
    {
        HttpStatusCode.OK or HttpStatusCode.NoContent => new ReviewCommentResult { Outcome = ReviewCommentOutcome.Success },
        HttpStatusCode.BadRequest => new ReviewCommentResult
        {
            Outcome = ReviewCommentOutcome.ValidationFailed,
            ErrorMessage = "The review comment was not accepted."
        },
        HttpStatusCode.Forbidden => new ReviewCommentResult
        {
            Outcome = ReviewCommentOutcome.Forbidden,
            ErrorMessage = "Only your own review comment, with no replies, can be changed on the current draft version."
        },
        HttpStatusCode.NotFound => new ReviewCommentResult
        {
            Outcome = ReviewCommentOutcome.NotFound,
            ErrorMessage = "The review comment no longer exists."
        },
        HttpStatusCode.Conflict => new ReviewCommentResult
        {
            Outcome = ReviewCommentOutcome.Conflict,
            ErrorMessage = "The review comment has been edited by another user."
        },
        _ => new ReviewCommentResult
        {
            Outcome = ReviewCommentOutcome.Error,
            ErrorMessage = "We could not complete the request. Try again later."
        }
    };

    /// <inheritdoc />
    public async Task<ProfileVersionAllRevisionDatesDto> GetProfileVersionAllRevisionDatesAsync(
        Guid profileVersionId,
        CancellationToken cancellationToken = default)
    {
        var result = await httpClient.GetFromJsonAsync<ProfileVersionAllRevisionDatesDto>(
            $"/api/profile-sections/all-revision-dates?profileVersionId={profileVersionId}",
            cancellationToken);

        return result ?? new ProfileVersionAllRevisionDatesDto { ProfileVersionId = profileVersionId };
    }

    /// <inheritdoc />
    public async Task<SaveRevisionDatesResult> SaveProfileVersionRevisionDatesAsync(
        Guid profileVersionId,
        IReadOnlyList<ProfileSectionRevisionDatesInputDto> sections,
        CancellationToken cancellationToken = default)
    {
        var request = new { ProfileVersionId = profileVersionId, Sections = sections };
        using var response = await httpClient.PutAsJsonAsync("/api/profile-sections/all-revision-dates", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var saved = await response.Content.ReadFromJsonAsync<SaveProfileRevisionDatesResultDto>(cancellationToken);

            return new SaveRevisionDatesResult
            {
                Outcome = SaveRevisionDatesOutcome.Success,
                SectionsSavedCount = saved?.SectionsSavedCount ?? 0
            };
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Conflict => new SaveRevisionDatesResult
            {
                Outcome = SaveRevisionDatesOutcome.Conflict,
                ErrorMessage = "Another user has changed these revision dates, or this version can no longer be edited. Your changes have not been saved; the latest values are shown."
            },
            HttpStatusCode.BadRequest => new SaveRevisionDatesResult
            {
                Outcome = SaveRevisionDatesOutcome.ValidationFailed,
                ErrorMessage = "The revision dates were not accepted. Check the values and try again."
            },
            _ => new SaveRevisionDatesResult
            {
                Outcome = SaveRevisionDatesOutcome.Error,
                ErrorMessage = "We could not save the revision dates. Try again later."
            }
        };
    }
}
