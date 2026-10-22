using System.Net;
using System.Text.RegularExpressions;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using LandingFakeApiClient = CDC.Web.Tests.Features.Landing.FakeApiClient;

namespace CDC.Web.Tests.Integration;

// Drives the real pipeline from the Manage profile Profile status dropdown to the message the legacy page showed.
public class ManageProfileStatusIntegrationTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid DraftStatusId = Guid.NewGuid();
    private static readonly Guid ValidationCompleteStatusId = Guid.NewGuid();
    private static readonly string PagePath = $"/SurveillanceProfiles/ManageProfile/{ProfileId}";

    [Fact]
    public async Task ProfileStatus_ListsEveryStatusWithTheProfilesCurrentOneSelected()
    {
        using var factory = CreateFactory(new LandingFakeApiClient(manageProfile: Profile(), profileStatusTypes: StatusTypes()));
        var client = await factory.SignedInClientAsync();

        var body = await client.GetStringAsync(PagePath);

        Assert.Contains("name=\"ProfileStatusId\"", body);
        Assert.Contains("<option value=\"\">Please select...</option>", body);
        Assert.Matches($"<option[^>]*selected[^>]*value=\"{DraftStatusId}\"[^>]*>Draft</option>", body);
        Assert.Contains($"<option value=\"{ValidationCompleteStatusId}\">Validation complete</option>", body);
        Assert.DoesNotContain("Successfully updated the profile status", body);
    }

    [Fact]
    public async Task ProfileStatus_SavesTheStatusChosenInTheDropdownAndSaysSo()
    {
        var apiClient = new LandingFakeApiClient(manageProfile: Profile(), profileStatusTypes: StatusTypes());
        using var factory = CreateFactory(apiClient);
        var client = await factory.SignedInClientAsync();

        var response = await PostStatusAsync(client, ValidationCompleteStatusId.ToString());
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((ProfileId, ValidationCompleteStatusId), apiClient.LastUpdatedProfileStatus);
        Assert.Contains("govuk-notification-banner--success", body);
        Assert.Contains("Successfully updated the profile status.", body);
        Assert.Matches($"<option[^>]*selected[^>]*value=\"{ValidationCompleteStatusId}\"[^>]*>Validation complete</option>", body);
    }

    // As in legacy, choosing "Please select..." sends an empty status for the data layer to refuse, rather than the page ruling it out.
    [Fact]
    public async Task ProfileStatus_SendsAnEmptyStatus_WhenPleaseSelectIsChosen()
    {
        var apiClient = new LandingFakeApiClient(manageProfile: Profile(), profileStatusTypes: StatusTypes());
        using var factory = CreateFactory(apiClient);
        var client = await factory.SignedInClientAsync();

        await PostStatusAsync(client, string.Empty);

        Assert.Equal((ProfileId, Guid.Empty), apiClient.LastUpdatedProfileStatus);
    }

    [Fact]
    public async Task ProfileStatus_ShowsTheError_AndNotTheSuccessMessage_WhenTheStatusCannotBeSaved()
    {
        const string message = "The profile status could not be saved. Please try again.";
        var apiClient = new LandingFakeApiClient(
            manageProfile: Profile(),
            profileStatusTypes: StatusTypes(),
            updateProfileStatusResult: new UpdateProfileStatusResult(UpdateProfileStatusOutcome.Error, message));
        using var factory = CreateFactory(apiClient);
        var client = await factory.SignedInClientAsync();

        var body = await (await PostStatusAsync(client, ValidationCompleteStatusId.ToString())).Content.ReadAsStringAsync();

        Assert.Contains(message, body);
        Assert.DoesNotContain("Successfully updated the profile status", body);
        Assert.DoesNotContain("govuk-notification-banner--success", body);
    }

    private static async Task<HttpResponseMessage> PostStatusAsync(HttpClient client, string profileStatusId)
    {
        var page = await client.GetStringAsync(PagePath);
        var token = Regex.Match(page, """name="__RequestVerificationToken"[^>]*value="([^"]+)""", RegexOptions.None, TimeSpan.FromSeconds(5));
        Assert.True(token.Success, "No antiforgery token found on the Manage profile page.");

        return await client.PostAsync(
            PagePath,
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token.Groups[1].Value,
                ["ProfileId"] = ProfileId.ToString(),
                ["ProfileStatusId"] = profileStatusId
            }));
    }

    private static ManageProfileViewModel Profile() => new()
    {
        ProfileId = ProfileId,
        ProfileTitle = "Anthrax",
        CurrentProfileVersionId = Guid.NewGuid(),
        LatestVersionId = Guid.NewGuid(),
        ProfileStatus = "Draft",
        ProfileStatusId = DraftStatusId
    };

    private static IReadOnlyList<ProfileStatusTypeDto> StatusTypes() =>
    [
        new ProfileStatusTypeDto { Id = DraftStatusId, Name = "Draft" },
        new ProfileStatusTypeDto { Id = ValidationCompleteStatusId, Name = "Validation complete", IsValidationComplete = true }
    ];

    private static WebApplicationFactory<Program> CreateFactory(LandingFakeApiClient apiClient) =>
        new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(apiClient);
            }));
}
