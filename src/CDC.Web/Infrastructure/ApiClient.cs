using System.Net;
using System.Net.Http.Json;
using CDC.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace CDC.Web.Infrastructure;

public interface IApiClient
{
    Task<ApiHealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets enriched profile search results, with version history, from
    /// <c>GET /api/profile-search/search</c>.</summary>
    Task<IReadOnlyList<ProfileSearchResultDto>> SearchProfilesAsync(
        string? searchText,
        bool displayPublished,
        bool displayDraft,
        bool displayScenarios,
        SearchForType searchForType,
        CancellationToken cancellationToken = default);

    /// <summary>Gets a profile's attributes from <c>GET /api/profiles/{profileId}/attributes</c>.</summary>
    /// <returns>The profile's attributes, or <see langword="null"/> when no such profile exists.</returns>
    Task<ProfileAttributesDto?> GetProfileAttributesAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>Updates a profile's title via <c>PUT /api/profiles/{profileId}</c>, leaving every
    /// other attribute unchanged.</summary>
    Task<UpdateProfileTitleResult> UpdateProfileTitleAsync(
        Guid profileId,
        string title,
        byte[] lastUpdated,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the details shown on the "Manage profile" page from
    /// <c>GET /api/profiles/{profileId}/manage</c>.</summary>
    Task<ManageProfileViewModel?> GetManageProfileAsync(Guid profileId, CancellationToken cancellationToken = default);

    /// <summary>Resolves (or provisions) the external user matching the given CIDM claims via
    /// <c>POST /api/users/external/resolve</c>.</summary>
    Task<ResolveExternalUserResult> ResolveExternalUserAsync(ResolveExternalUserRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Resolves the internal user matching the given Entra ID claims via
    /// <c>POST /api/users/internal/resolve</c>.</summary>
    Task<ResolveInternalUserResult> ResolveInternalUserAsync(ResolveInternalUserRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>Gets every profile status a profile can be set to, from <c>GET /api/profiles/status-types</c>.</summary>
    Task<IReadOnlyList<ProfileStatusTypeDto>> GetProfileStatusTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>Updates a profile's status via <c>PUT /api/profiles/{profileId}/status</c>.</summary>
    Task<UpdateProfileStatusResult> UpdateProfileStatusAsync(
        Guid profileId,
        Guid profileStatusId,
        CancellationToken cancellationToken = default);

    /// <summary>Gets the current public static reports or manuals from <c>GET /api/static-reports</c>.</summary>
    Task<IReadOnlyList<StaticReportListItemDto>> GetCurrentStaticReportsAsync(
        bool isUserManual = false,
        bool publicOnly = true,
        CancellationToken cancellationToken = default);

    /// <summary>Gets every version of one static report or manual from
    /// <c>GET /api/static-reports/{staticReportId}/history</c>.</summary>
    Task<IReadOnlyList<StaticReportListItemDto>> GetStaticReportHistoryAsync(
        Guid staticReportId,
        bool publicOnly = true,
        CancellationToken cancellationToken = default);

    /// <summary>Gets whether the current user may upload static reports or user manuals from
    /// <c>GET /api/static-reports/upload-permission</c>.</summary>
    Task<bool> CanUploadStaticReportsAsync(CancellationToken cancellationToken = default);

    /// <summary>Uploads a new static report or user manual version via <c>POST /api/static-reports</c>.</summary>
    Task<UploadStaticReportResult> UploadStaticReportAsync(
        string title,
        byte[] pdfData,
        bool isUserManual,
        bool isPublic,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a static report or user manual version via
    /// <c>DELETE /api/static-reports/versions/{staticReportVersionId}</c>.</summary>
    Task<DeleteStaticReportVersionResult> DeleteStaticReportVersionAsync(
        Guid staticReportVersionId,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a new version of a profile via <c>POST /api/profiles/versions</c>.</summary>
    /// <param name="profileVersionId">The profile version to base the new version on. Must be the latest version.</param>
    /// <param name="isPublished">Whether the new version is published rather than a draft.</param>
    /// <param name="isPublic">Whether the new version is publicly visible. Only valid when <paramref name="isPublished"/> is <see langword="true"/>.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    Task<CreateNewProfileVersionResult> CreateNewProfileVersionAsync(
        Guid profileVersionId,
        bool isPublished,
        bool isPublic,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes a profile version via <c>DELETE /api/profiles/versions/{profileVersionId}</c>.</summary>
    Task<DeleteProfileVersionResult> DeleteProfileVersionAsync(Guid profileVersionId, CancellationToken cancellationToken = default);
}

// Thin typed HttpClient wrapper around CDC.Api. All business-logic/data calls from CDC.Web go through
// an interface like this rather than talking to the database directly.
public sealed class ApiClient(HttpClient httpClient) : IApiClient
{
    public Task<ApiHealthResponse?> GetHealthAsync(CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<ApiHealthResponse>("/health", cancellationToken);

    public async Task<IReadOnlyList<ProfileSearchResultDto>> SearchProfilesAsync(
        string? searchText,
        bool displayPublished,
        bool displayDraft,
        bool displayScenarios,
        SearchForType searchForType,
        CancellationToken cancellationToken = default)
    {
        var url = QueryHelpers.AddQueryString("/api/profile-search/search", new Dictionary<string, string?>
        {
            ["searchText"] = searchText,
            ["displayPublished"] = displayPublished.ToString(),
            ["displayDraft"] = displayDraft.ToString(),
            ["displayScenarios"] = displayScenarios.ToString(),
            ["searchForType"] = searchForType.ToString()
        });

        var results = await httpClient.GetFromJsonAsync<IReadOnlyList<ProfileSearchResultDto>>(url, cancellationToken);

        return results ?? [];
    }

    public async Task<ProfileAttributesDto?> GetProfileAttributesAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/profiles/{profileId}/attributes", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ProfileAttributesDto>(cancellationToken);
    }

    public async Task<UpdateProfileTitleResult> UpdateProfileTitleAsync(
        Guid profileId,
        string title,
        byte[] lastUpdated,
        CancellationToken cancellationToken = default)
    {
        var request = new UpdateProfileTitleRequest { Id = profileId, Title = title, LastUpdated = lastUpdated };
        var response = await httpClient.PutAsJsonAsync($"/api/profiles/{profileId}", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new UpdateProfileTitleResult(UpdateProfileTitleOutcome.Success, null);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
            return new UpdateProfileTitleResult(
                UpdateProfileTitleOutcome.Conflict,
                problem?.Detail ?? "This profile has been edited by another user. Reload the page and try again.");
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var message = problem?.Errors.Count > 0
                ? string.Join(" ", problem.Errors.SelectMany(error => error.Value))
                : "The profile title could not be saved.";

            return new UpdateProfileTitleResult(UpdateProfileTitleOutcome.ValidationFailed, message);
        }

        return new UpdateProfileTitleResult(UpdateProfileTitleOutcome.Error, "The profile title could not be saved. Please try again.");
    }

    public async Task<CreateNewProfileVersionResult> CreateNewProfileVersionAsync(
        Guid profileVersionId,
        bool isPublished,
        bool isPublic,
        CancellationToken cancellationToken = default)
    {
        var request = new { ProfileVersionId = profileVersionId, IsPublished = isPublished, IsPublic = isPublic };
        var response = await httpClient.PostAsJsonAsync("/api/profiles/versions", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<NewProfileVersionResultDto>(cancellationToken);
            return new CreateNewProfileVersionResult(CreateNewProfileVersionOutcome.Success, result?.NewProfileVersionId, null);
        }

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
            return new CreateNewProfileVersionResult(
                CreateNewProfileVersionOutcome.Conflict,
                null,
                problem?.Detail ?? "This profile version is not eligible for a new draft version.");
        }

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(cancellationToken);
            var message = problem?.Errors.Count > 0
                ? string.Join(" ", problem.Errors.SelectMany(error => error.Value))
                : "The new draft version could not be created.";

            return new CreateNewProfileVersionResult(CreateNewProfileVersionOutcome.ValidationFailed, null, message);
        }

        return new CreateNewProfileVersionResult(
            CreateNewProfileVersionOutcome.Error, null, "The new draft version could not be created. Please try again.");
    }

    public async Task<DeleteProfileVersionResult> DeleteProfileVersionAsync(
        Guid profileVersionId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/api/profiles/versions/{profileVersionId}", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<DeleteProfileVersionResultDto>(cancellationToken);
            return new DeleteProfileVersionResult(DeleteProfileVersionOutcome.Success, result?.IsProfileDeleted ?? false, null);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
            return new DeleteProfileVersionResult(
                DeleteProfileVersionOutcome.NotFound,
                false,
                problem?.Detail ?? "This profile version could not be found. Another user may have already deleted it.");
        }

        return new DeleteProfileVersionResult(
            DeleteProfileVersionOutcome.Error, false, "The profile version could not be deleted. Please try again.");
    }

    public async Task<ManageProfileViewModel?> GetManageProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"/api/profiles/{profileId}/manage", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<ManageProfileViewModel>(cancellationToken);
    }

    public async Task<ResolveExternalUserResult> ResolveExternalUserAsync(
        ResolveExternalUserRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/users/external/resolve", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var user = await response.Content.ReadFromJsonAsync<ExternalUserDto>(cancellationToken);
            return new ResolveExternalUserResult(ResolveExternalUserOutcome.Success, user);
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return new ResolveExternalUserResult(ResolveExternalUserOutcome.NotPermitted, null);
        }

        return new ResolveExternalUserResult(ResolveExternalUserOutcome.Error, null);
    }

    public async Task<ResolveInternalUserResult> ResolveInternalUserAsync(
        ResolveInternalUserRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("/api/users/internal/resolve", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var user = await response.Content.ReadFromJsonAsync<InternalUserDto>(cancellationToken);
            return new ResolveInternalUserResult(ResolveInternalUserOutcome.Success, user);
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new ResolveInternalUserResult(ResolveInternalUserOutcome.NotPermitted, null);
        }

        return new ResolveInternalUserResult(ResolveInternalUserOutcome.Error, null);
    }

    public async Task<IReadOnlyList<ProfileStatusTypeDto>> GetProfileStatusTypesAsync(CancellationToken cancellationToken = default)
    {
        var statusTypes = await httpClient.GetFromJsonAsync<IReadOnlyList<ProfileStatusTypeDto>>("/api/profiles/status-types", cancellationToken);

        return statusTypes ?? [];
    }

    public async Task<UpdateProfileStatusResult> UpdateProfileStatusAsync(
        Guid profileId,
        Guid profileStatusId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"/api/profiles/{profileId}/status",
            new { ProfileStatusId = profileStatusId },
            cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new UpdateProfileStatusResult(UpdateProfileStatusOutcome.Success, null);
        }

        return response.StatusCode switch
        {
            HttpStatusCode.NotFound => new UpdateProfileStatusResult(
                UpdateProfileStatusOutcome.NotFound,
                "The selected profile status could not be found."),
            _ => new UpdateProfileStatusResult(
                UpdateProfileStatusOutcome.Error,
                "The profile status could not be saved. Please try again.")
        };
    }

    public async Task<IReadOnlyList<StaticReportListItemDto>> GetCurrentStaticReportsAsync(
        bool isUserManual = false,
        bool publicOnly = true,
        CancellationToken cancellationToken = default)
    {
        var url = QueryHelpers.AddQueryString("/api/static-reports", new Dictionary<string, string?>
        {
            ["isUserManual"] = isUserManual.ToString(),
            ["publicOnly"] = publicOnly.ToString()
        });

        var reports = await httpClient.GetFromJsonAsync<IReadOnlyList<StaticReportListItemDto>>(url, cancellationToken);
        return reports ?? [];
    }

    public async Task<IReadOnlyList<StaticReportListItemDto>> GetStaticReportHistoryAsync(
        Guid staticReportId,
        bool publicOnly = true,
        CancellationToken cancellationToken = default)
    {
        var url = QueryHelpers.AddQueryString(
            $"/api/static-reports/{staticReportId}/history",
            new Dictionary<string, string?> { ["publicOnly"] = publicOnly.ToString() });

        var versions = await httpClient.GetFromJsonAsync<IReadOnlyList<StaticReportListItemDto>>(url, cancellationToken);
        return versions ?? [];
    }

    public async Task<bool> CanUploadStaticReportsAsync(CancellationToken cancellationToken = default)
    {
        var permission = await httpClient.GetFromJsonAsync<StaticReportUploadPermissionDto>(
            "/api/static-reports/upload-permission", cancellationToken);

        return permission?.CanUpload ?? false;
    }

    public async Task<UploadStaticReportResult> UploadStaticReportAsync(
        string title,
        byte[] pdfData,
        bool isUserManual,
        bool isPublic,
        CancellationToken cancellationToken = default)
    {
        var request = new { Title = title, PdfData = pdfData, IsUserManual = isUserManual, IsPublic = isPublic };
        var response = await httpClient.PostAsJsonAsync("/api/static-reports", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new UploadStaticReportResult(UploadStaticReportOutcome.Success, null);
        }

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);

        return response.StatusCode switch
        {
            HttpStatusCode.Forbidden => new UploadStaticReportResult(
                UploadStaticReportOutcome.Forbidden, problem?.Detail ?? "You do not have permission to upload documents."),
            _ => new UploadStaticReportResult(
                UploadStaticReportOutcome.ValidationFailed, problem?.Detail ?? "The document could not be uploaded. Please try again.")
        };
    }

    public async Task<DeleteStaticReportVersionResult> DeleteStaticReportVersionAsync(
        Guid staticReportVersionId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync($"/api/static-reports/versions/{staticReportVersionId}", cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new DeleteStaticReportVersionResult(DeleteStaticReportVersionOutcome.Success, null);
        }

        if (response.StatusCode == HttpStatusCode.Forbidden)
        {
            return new DeleteStaticReportVersionResult(
                DeleteStaticReportVersionOutcome.Forbidden, "You do not have permission to delete this document.");
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new DeleteStaticReportVersionResult(
                DeleteStaticReportVersionOutcome.NotFound, "This document could not be found. Another user may have already deleted it.");
        }

        return new DeleteStaticReportVersionResult(
            DeleteStaticReportVersionOutcome.Error, "The document could not be deleted. Please try again.");
    }
}

public sealed record ApiHealthResponse(string? Status, double UptimeSeconds, DateTime TimestampUtc);

/// <summary>Outcome of <see cref="IApiClient.UpdateProfileTitleAsync"/>.</summary>
public enum UpdateProfileTitleOutcome
{
    Success,
    ValidationFailed,
    Conflict,
    Error
}

/// <summary>Result of attempting to update a profile's title.</summary>
public sealed record UpdateProfileTitleResult(UpdateProfileTitleOutcome Outcome, string? ErrorMessage);
