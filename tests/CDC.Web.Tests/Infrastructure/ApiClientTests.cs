using System.Net;
using System.Text;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class ApiClientTests
{
    [Fact]
    public async Task GetHealthAsync_DeserialisesTheResponseBody()
    {
        const string json = """{"status":"Healthy","uptimeSeconds":12.5,"timestampUtc":"2026-01-01T00:00:00Z"}""";
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var health = await client.GetHealthAsync();

        Assert.NotNull(health);
        Assert.Equal("Healthy", health.Status);
        Assert.Equal("/health", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task SearchProfilesAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            [
              {
                "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
                "title": "Bovine tuberculosis",
                "status": "Published",
                "createdAtUtc": "2026-01-01T00:00:00Z",
                "modifiedAtUtc": "2026-01-02T00:00:00Z",
                "isPublic": true,
                "affectedSpecies": [],
                "publishedVersions": [],
                "draftVersions": [],
                "whatIfScenarios": []
              }
            ]
            """;
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var results = await client.SearchProfilesAsync("tb", true, false, true, SearchForType.ExactWordOrPhrase);

        var item = Assert.Single(results);
        Assert.Equal("Bovine tuberculosis", item.Title);
    }

    [Fact]
    public async Task SearchProfilesAsync_BuildsTheQueryStringFromEveryFilter()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, "[]");
        var client = CreateClient(handler);

        await client.SearchProfilesAsync("tb", true, false, true, SearchForType.AllWords);

        var query = handler.LastRequestUri!.Query;
        Assert.Contains("searchText=tb", query);
        Assert.Contains("displayPublished=True", query);
        Assert.Contains("displayDraft=False", query);
        Assert.Contains("displayScenarios=True", query);
        Assert.Contains("searchForType=AllWords", query);
    }

    [Fact]
    public async Task SearchProfilesAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.OK, "null"));

        var results = await client.SearchProfilesAsync(null, true, false, false, SearchForType.ExactWordOrPhrase);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchProfilesAsync_Throws_OnANonSuccessStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.SearchProfilesAsync(null, true, false, false, SearchForType.ExactWordOrPhrase));
    }

    [Fact]
    public async Task GetProfileAttributesAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            {
              "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
              "title": "Bovine tuberculosis",
              "lastUpdated": "AQIDBAUGBwg="
            }
            """;
        var profileId = Guid.NewGuid();
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var attributes = await client.GetProfileAttributesAsync(profileId);

        Assert.NotNull(attributes);
        Assert.Equal("Bovine tuberculosis", attributes.Title);
        Assert.Equal(8, attributes.LastUpdated.Length);
        Assert.Equal($"/api/profiles/{profileId}/attributes", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetProfileAttributesAsync_ReturnsNull_WhenTheProfileDoesNotExist()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.NotFound, string.Empty));

        var attributes = await client.GetProfileAttributesAsync(Guid.NewGuid());

        Assert.Null(attributes);
    }

    [Fact]
    public async Task GetProfileAttributesAsync_Throws_OnANonSuccessNonNotFoundStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetProfileAttributesAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetManageProfileAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            {
              "profileId": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
              "profileTitle": "Bovine Tuberculosis",
              "scenarioTitle": "Default Scenario",
              "latestPublishedVersionPublic": "Version 5",
              "latestPublishedVersionDefraNetOnly": "Version 7",
              "latestDraftVersion": "Version 8",
              "profileStatus": "Draft"
            }
            """;
        var profileId = Guid.NewGuid();
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var profile = await client.GetManageProfileAsync(profileId);

        Assert.NotNull(profile);
        Assert.Equal("Bovine Tuberculosis", profile.ProfileTitle);
        Assert.Equal("Draft", profile.ProfileStatus);
        Assert.Equal($"/api/profiles/{profileId}/manage", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetManageProfileAsync_ReturnsNull_WhenTheProfileDoesNotExist()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.NotFound, string.Empty));

        var profile = await client.GetManageProfileAsync(Guid.NewGuid());

        Assert.Null(profile);
    }

    [Fact]
    public async Task GetManageProfileAsync_Throws_OnANonSuccessNonNotFoundStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetManageProfileAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetProfileStatusTypesAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            [ { "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f", "name": "Draft", "isValidationComplete": false } ]
            """;
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var statusTypes = await client.GetProfileStatusTypesAsync();

        var statusType = Assert.Single(statusTypes);
        Assert.Equal("Draft", statusType.Name);
        Assert.Equal("/api/profiles/status-types", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetProfileStatusTypesAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.OK, "null"));

        var statusTypes = await client.GetProfileStatusTypesAsync();

        Assert.Empty(statusTypes);
    }

    [Fact]
    public async Task UpdateProfileStatusAsync_ReturnsSuccess_OnNoContent()
    {
        var profileId = Guid.NewGuid();
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.NoContent, string.Empty);
        var client = CreateClient(handler);

        var result = await client.UpdateProfileStatusAsync(profileId, Guid.NewGuid());

        Assert.Equal(UpdateProfileStatusOutcome.Success, result.Outcome);
        Assert.Equal($"/api/profiles/{profileId}/status", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task UpdateProfileStatusAsync_ReturnsNotFound_OnHttp404()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.NotFound, string.Empty));

        var result = await client.UpdateProfileStatusAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(UpdateProfileStatusOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task UpdateProfileStatusAsync_ReturnsError_OnUnexpectedStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        var result = await client.UpdateProfileStatusAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(UpdateProfileStatusOutcome.Error, result.Outcome);
    }

    [Fact]
    public async Task UpdateProfileTitleAsync_ReturnsSuccess_OnNoContent()
    {
        var profileId = Guid.NewGuid();
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.NoContent, string.Empty);
        var client = CreateClient(handler);

        var result = await client.UpdateProfileTitleAsync(profileId, "New title", [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Equal(UpdateProfileTitleOutcome.Success, result.Outcome);
        Assert.Null(result.ErrorMessage);
        Assert.Equal($"/api/profiles/{profileId}", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task UpdateProfileTitleAsync_ReturnsConflict_OnHttpConflict()
    {
        const string json = """{"detail":"Another user has updated this profile."}""";
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.Conflict, json));

        var result = await client.UpdateProfileTitleAsync(Guid.NewGuid(), "New title", [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Equal(UpdateProfileTitleOutcome.Conflict, result.Outcome);
        Assert.Equal("Another user has updated this profile.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateProfileTitleAsync_ReturnsConflict_WithDefaultMessage_WhenProblemDetailHasNoDetail()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.Conflict, "{}"));

        var result = await client.UpdateProfileTitleAsync(Guid.NewGuid(), "New title", [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Equal(UpdateProfileTitleOutcome.Conflict, result.Outcome);
        Assert.Equal("This profile has been edited by another user. Reload the page and try again.", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateProfileTitleAsync_ReturnsValidationFailed_OnBadRequestWithErrors()
    {
        const string json = """{"errors":{"Title":["You must enter a profile title"]}}""";
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.BadRequest, json));

        var result = await client.UpdateProfileTitleAsync(Guid.NewGuid(), string.Empty, [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Equal(UpdateProfileTitleOutcome.ValidationFailed, result.Outcome);
        Assert.Equal("You must enter a profile title", result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateProfileTitleAsync_ReturnsError_OnAnyOtherStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        var result = await client.UpdateProfileTitleAsync(Guid.NewGuid(), "New title", [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Equal(UpdateProfileTitleOutcome.Error, result.Outcome);
        Assert.Equal("The profile title could not be saved. Please try again.", result.ErrorMessage);
    }

    [Fact]
    public async Task ResolveExternalUserAsync_ReturnsSuccess_WithDeserialisedUser()
    {
        const string json = """
            {
              "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
              "fullName": "Jane External",
              "emailAddress": "user@example.com",
              "organisation": "ACME Ltd"
            }
            """;
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);
        var request = new ResolveExternalUserRequestDto
        {
            SsoUserIdExt = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "Jane",
            LastName = "External",
            Organisation = "ACME Ltd"
        };

        var result = await client.ResolveExternalUserAsync(request);

        Assert.Equal(ResolveExternalUserOutcome.Success, result.Outcome);
        Assert.Equal("Jane External", result.User!.FullName);
        Assert.Equal("/api/users/external/resolve", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ResolveExternalUserAsync_ReturnsNotPermitted_OnForbidden()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.Forbidden, "{}"));
        var request = new ResolveExternalUserRequestDto
        {
            SsoUserIdExt = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "Jane",
            LastName = "External",
            Organisation = "ACME Ltd"
        };

        var result = await client.ResolveExternalUserAsync(request);

        Assert.Equal(ResolveExternalUserOutcome.NotPermitted, result.Outcome);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task ResolveExternalUserAsync_ReturnsError_OnAnyOtherStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));
        var request = new ResolveExternalUserRequestDto
        {
            SsoUserIdExt = Guid.NewGuid(),
            Email = "user@example.com",
            FirstName = "Jane",
            LastName = "External",
            Organisation = "ACME Ltd"
        };

        var result = await client.ResolveExternalUserAsync(request);

        Assert.Equal(ResolveExternalUserOutcome.Error, result.Outcome);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task ResolveInternalUserAsync_ReturnsSuccess_WithDeserialisedUser()
    {
        const string json = """
            {
              "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
              "fullName": "Jane Internal",
              "isProfileEditor": true,
              "isPolicyProfileUser": false
            }
            """;
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);
        var request = new ResolveInternalUserRequestDto
        {
            SsoUserIdInt = Guid.NewGuid(),
            UserName = @"DEFRA\jdoe",
            FullName = "Jane Internal"
        };

        var result = await client.ResolveInternalUserAsync(request);

        Assert.Equal(ResolveInternalUserOutcome.Success, result.Outcome);
        Assert.Equal("Jane Internal", result.User!.FullName);
        Assert.Equal("/api/users/internal/resolve", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ResolveInternalUserAsync_ReturnsNotPermitted_OnNotFound()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.NotFound, "{}"));
        var request = new ResolveInternalUserRequestDto
        {
            SsoUserIdInt = Guid.NewGuid(),
            UserName = @"DEFRA\jdoe",
            FullName = "Jane Internal"
        };

        var result = await client.ResolveInternalUserAsync(request);

        Assert.Equal(ResolveInternalUserOutcome.NotPermitted, result.Outcome);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task ResolveInternalUserAsync_ReturnsError_OnAnyOtherStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));
        var request = new ResolveInternalUserRequestDto
        {
            SsoUserIdInt = Guid.NewGuid(),
            UserName = @"DEFRA\jdoe",
            FullName = "Jane Internal"
        };

        var result = await client.ResolveInternalUserAsync(request);

        Assert.Equal(ResolveInternalUserOutcome.Error, result.Outcome);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task GetCurrentStaticReportsAsync_ReturnsReports_OnSuccess()
    {
        const string json = """
            [{"id":"11111111-1111-1111-1111-111111111111","staticReportId":"22222222-2222-2222-2222-222222222222","title":"Help using D2R2","versionMajor":1,"effectiveDateFrom":"2024-01-01T00:00:00Z","effectiveDateTo":"2025-01-01T00:00:00Z","isUserManual":true,"isPublic":true,"fileSize":1024}]
            """;
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var reports = await client.GetCurrentStaticReportsAsync(isUserManual: true, publicOnly: false);

        var report = Assert.Single(reports);
        Assert.Equal("Help using D2R2", report.Title);
        Assert.False(report.IsCurrent);
        Assert.Equal("/api/static-reports", handler.LastRequestUri!.AbsolutePath);
        Assert.Contains("isUserManual=True", handler.LastRequestUri.Query);
        Assert.Contains("publicOnly=False", handler.LastRequestUri.Query);
    }

    [Fact]
    public async Task GetCurrentStaticReportsAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.OK, "null"));

        var reports = await client.GetCurrentStaticReportsAsync();

        Assert.Empty(reports);
    }

    [Fact]
    public async Task GetStaticReportHistoryAsync_ReturnsVersions_OnSuccess()
    {
        var staticReportId = Guid.NewGuid();
        var json = $$"""
            [{"id":"11111111-1111-1111-1111-111111111111","staticReportId":"{{staticReportId}}","title":"Help using D2R2","versionMajor":2,"effectiveDateFrom":"2024-01-01T00:00:00Z","isUserManual":true,"isPublic":false,"fileSize":2048}]
            """;
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var versions = await client.GetStaticReportHistoryAsync(staticReportId, publicOnly: false);

        var version = Assert.Single(versions);
        Assert.Equal(2, version.VersionMajor);
        Assert.Equal($"/api/static-reports/{staticReportId}/history", handler.LastRequestUri!.AbsolutePath);
        Assert.Contains("publicOnly=False", handler.LastRequestUri.Query);
    }

    [Fact]
    public async Task GetStaticReportHistoryAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.OK, "null"));

        var versions = await client.GetStaticReportHistoryAsync(Guid.NewGuid());

        Assert.Empty(versions);
    }

    [Fact]
    public async Task CanUploadStaticReportsAsync_ReturnsTrue_WhenPermitted()
    {
        const string json = """{"canUpload":true}""";
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var canUpload = await client.CanUploadStaticReportsAsync();

        Assert.True(canUpload);
        Assert.Equal("/api/static-reports/upload-permission", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CanUploadStaticReportsAsync_ReturnsFalse_WhenTheResponseBodyIsNull()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.OK, "null"));

        var canUpload = await client.CanUploadStaticReportsAsync();

        Assert.False(canUpload);
    }

    [Fact]
    public async Task UploadStaticReportAsync_ReturnsSuccess_OnNoContent()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.NoContent, string.Empty);
        var client = CreateClient(handler);

        var result = await client.UploadStaticReportAsync("Help using D2R2", [1, 2, 3], isUserManual: true, isPublic: false);

        Assert.Equal(UploadStaticReportOutcome.Success, result.Outcome);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("/api/static-reports", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task UploadStaticReportAsync_ReturnsForbidden_OnHttpForbidden()
    {
        const string json = """{"detail":"You do not have permission to upload documents."}""";
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.Forbidden, json));

        var result = await client.UploadStaticReportAsync("Help using D2R2", [1, 2, 3], isUserManual: true, isPublic: false);

        Assert.Equal(UploadStaticReportOutcome.Forbidden, result.Outcome);
        Assert.Equal("You do not have permission to upload documents.", result.ErrorMessage);
    }

    [Fact]
    public async Task UploadStaticReportAsync_ReturnsValidationFailed_OnAnyOtherStatusCode()
    {
        const string json = """{"detail":"Please choose a file to upload."}""";
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.BadRequest, json));

        var result = await client.UploadStaticReportAsync(string.Empty, [], isUserManual: true, isPublic: false);

        Assert.Equal(UploadStaticReportOutcome.ValidationFailed, result.Outcome);
        Assert.Equal("Please choose a file to upload.", result.ErrorMessage);
    }

    [Fact]
    public async Task DeleteStaticReportVersionAsync_ReturnsSuccess_OnNoContent()
    {
        var staticReportVersionId = Guid.NewGuid();
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.NoContent, string.Empty);
        var client = CreateClient(handler);

        var result = await client.DeleteStaticReportVersionAsync(staticReportVersionId);

        Assert.Equal(DeleteStaticReportVersionOutcome.Success, result.Outcome);
        Assert.Null(result.ErrorMessage);
        Assert.Equal($"/api/static-reports/versions/{staticReportVersionId}", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task DeleteStaticReportVersionAsync_ReturnsForbidden_OnHttpForbidden()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.Forbidden, string.Empty));

        var result = await client.DeleteStaticReportVersionAsync(Guid.NewGuid());

        Assert.Equal(DeleteStaticReportVersionOutcome.Forbidden, result.Outcome);
        Assert.Equal("You do not have permission to delete this document.", result.ErrorMessage);
    }

    [Fact]
    public async Task DeleteStaticReportVersionAsync_ReturnsNotFound_OnHttp404()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.NotFound, string.Empty));

        var result = await client.DeleteStaticReportVersionAsync(Guid.NewGuid());

        Assert.Equal(DeleteStaticReportVersionOutcome.NotFound, result.Outcome);
        Assert.Equal("This document could not be found. Another user may have already deleted it.", result.ErrorMessage);
    }

    [Fact]
    public async Task DeleteStaticReportVersionAsync_ReturnsError_OnAnyOtherStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        var result = await client.DeleteStaticReportVersionAsync(Guid.NewGuid());

        Assert.Equal(DeleteStaticReportVersionOutcome.Error, result.Outcome);
        Assert.Equal("The document could not be deleted. Please try again.", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateNewProfileVersionAsync_ReturnsSuccess_WithNewVersionId_OnOk()
    {
        var newVersionId = Guid.NewGuid();
        var json = $$"""{"newProfileVersionId":"{{newVersionId}}"}""";
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var result = await client.CreateNewProfileVersionAsync(Guid.NewGuid(), isPublished: false, isPublic: false);

        Assert.Equal(CreateNewProfileVersionOutcome.Success, result.Outcome);
        Assert.Equal(newVersionId, result.NewProfileVersionId);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("/api/profiles/versions", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task CreateNewProfileVersionAsync_ReturnsConflict_OnHttpConflict()
    {
        const string json = """{"detail":"This profile version is not eligible for a new draft version."}""";
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.Conflict, json));

        var result = await client.CreateNewProfileVersionAsync(Guid.NewGuid(), isPublished: false, isPublic: false);

        Assert.Equal(CreateNewProfileVersionOutcome.Conflict, result.Outcome);
        Assert.Equal("This profile version is not eligible for a new draft version.", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateNewProfileVersionAsync_ReturnsValidationFailed_OnBadRequestWithErrors()
    {
        const string json = """{"errors":{"ProfileVersionId":["The profile version could not be found."]}}""";
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.BadRequest, json));

        var result = await client.CreateNewProfileVersionAsync(Guid.NewGuid(), isPublished: false, isPublic: false);

        Assert.Equal(CreateNewProfileVersionOutcome.ValidationFailed, result.Outcome);
        Assert.Equal("The profile version could not be found.", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateNewProfileVersionAsync_ReturnsError_OnAnyOtherStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        var result = await client.CreateNewProfileVersionAsync(Guid.NewGuid(), isPublished: false, isPublic: false);

        Assert.Equal(CreateNewProfileVersionOutcome.Error, result.Outcome);
        Assert.Equal("The new draft version could not be created. Please try again.", result.ErrorMessage);
    }

    [Fact]
    public async Task DeleteProfileVersionAsync_ReturnsSuccess_WithIsProfileDeleted_OnOk()
    {
        var profileVersionId = Guid.NewGuid();
        const string json = """{"isProfileDeleted":true}""";
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var result = await client.DeleteProfileVersionAsync(profileVersionId);

        Assert.Equal(DeleteProfileVersionOutcome.Success, result.Outcome);
        Assert.True(result.IsProfileDeleted);
        Assert.Null(result.ErrorMessage);
        Assert.Equal($"/api/profiles/versions/{profileVersionId}", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task DeleteProfileVersionAsync_ReturnsNotFound_OnHttp404()
    {
        const string json = """{"detail":"This profile version could not be found."}""";
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.NotFound, json));

        var result = await client.DeleteProfileVersionAsync(Guid.NewGuid());

        Assert.Equal(DeleteProfileVersionOutcome.NotFound, result.Outcome);
        Assert.False(result.IsProfileDeleted);
        Assert.Equal("This profile version could not be found.", result.ErrorMessage);
    }

    [Fact]
    public async Task DeleteProfileVersionAsync_ReturnsError_OnAnyOtherStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        var result = await client.DeleteProfileVersionAsync(Guid.NewGuid());

        Assert.Equal(DeleteProfileVersionOutcome.Error, result.Outcome);
        Assert.Equal("The profile version could not be deleted. Please try again.", result.ErrorMessage);
    }

    private static ApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") };

        return new ApiClient(httpClient);
    }

    private sealed class RecordingHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
