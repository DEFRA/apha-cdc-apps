using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CDC.Common.Contracts;
using CDC.Web.Models;

namespace CDC.Web.Infrastructure;

/// <summary>
/// Default <see cref="IProfileQuestionsApiService"/>. The base address is configured on the underlying
/// <see cref="HttpClient"/> in Program.cs (from <c>Api:BaseUrl</c>), so no URL is hardcoded here. Read failures
/// are left to propagate so the page can decide how to present them.
/// </summary>
/// <param name="httpClient">Typed client pointing at CDC.Api.</param>
public sealed class ProfileQuestionsApiService(HttpClient httpClient) : IProfileQuestionsApiService
{
    /// <summary>The message shown when another user saved the question first.</summary>
    public const string ConflictMessage = "The user guidance has been edited by another user";

    /// <summary>The message shown when the user may not edit user guidance.</summary>
    public const string ForbiddenMessage = "You do not have permission to edit user guidance";

    /// <summary>The message shown when the question no longer exists.</summary>
    public const string NotFoundMessage = "The question could not be found";

    /// <summary>The message shown when CDC.Api rejects the change and gives no reason.</summary>
    public const string ValidationFallbackMessage = "The question text or guidance was not accepted";

    /// <summary>The message shown when the save fails for any other reason.</summary>
    public const string ErrorMessage = "We could not save the change. Try again later";

    /// <inheritdoc />
    public async Task<ProfileQuestionEditDto?> GetProfileQuestionAsync(Guid profileQuestionId, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync($"/api/profile-questions/{profileQuestionId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ProfileQuestionEditDto>(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<ProfileGuidanceContentResult> GetProfileGuidanceContentAsync(CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync("/api/profile-questions/guidance-content", cancellationToken);

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return new ProfileGuidanceContentResult(ProfileGuidanceContentOutcome.Forbidden);
        }

        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadFromJsonAsync<ProfileGuidanceContentDto>(cancellationToken);

        return new ProfileGuidanceContentResult(ProfileGuidanceContentOutcome.Success, content ?? new ProfileGuidanceContentDto());
    }

    /// <inheritdoc />
    public async Task<ProfileQuestionUpdateResult> UpdateProfileQuestionAsync(
        Guid profileQuestionId,
        UpdateProfileQuestionRequest request,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PutAsJsonAsync($"/api/profile-questions/{profileQuestionId}", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new ProfileQuestionUpdateResult { Outcome = ProfileQuestionUpdateOutcome.Success };
        }

        return response.StatusCode switch
        {
            HttpStatusCode.Conflict => Failure(ProfileQuestionUpdateOutcome.Conflict, ConflictMessage),
            HttpStatusCode.Forbidden => Failure(ProfileQuestionUpdateOutcome.Forbidden, ForbiddenMessage),
            HttpStatusCode.NotFound => Failure(ProfileQuestionUpdateOutcome.NotFound, NotFoundMessage),
            HttpStatusCode.BadRequest => Failure(
                ProfileQuestionUpdateOutcome.ValidationFailed,
                await ReadFirstValidationMessageAsync(response, cancellationToken)),
            _ => Failure(ProfileQuestionUpdateOutcome.Error, ErrorMessage)
        };
    }

    private static ProfileQuestionUpdateResult Failure(ProfileQuestionUpdateOutcome outcome, string message) =>
        new() { Outcome = outcome, ErrorMessage = message };

    /// <summary>Reads the first reason from a 400 response's validation problem body, if it has one.</summary>
    private static async Task<string> ReadFirstValidationMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemBody>(cancellationToken);

            return problem?.Errors?.Values.SelectMany(messages => messages).FirstOrDefault(message => !string.IsNullOrWhiteSpace(message))
                ?? ValidationFallbackMessage;
        }
        catch (JsonException)
        {
            return ValidationFallbackMessage;
        }
    }

    private sealed record ValidationProblemBody(Dictionary<string, string[]>? Errors);
}
