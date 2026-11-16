using System.Net;
using System.Text.RegularExpressions;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// Drives the real pipeline for the Manage profile "Allow public access" link: the legacy lnkAllowPublicAccess confirmed, made the current
// published version public and redisplayed Manage profile (it did not leave the page). The link used to open a placeholder page instead.
public class ManageProfileAllowPublicAccessIntegrationTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid PublishedVersionId = Guid.NewGuid();

    [Fact]
    public async Task ManageProfile_OffersAllowPublicAccessAsAConfirmedAction_NotALinkToAnotherPage()
    {
        var apiClient = new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: Profile(canAllowPublicAccess: true));
        using var factory = CreateFactory(apiClient);
        var client = await SignedInClientWithoutRedirectsAsync(factory);

        var body = await client.GetStringAsync($"/SurveillanceProfiles/ManageProfile/{ProfileId}");

        Assert.Contains("""data-confirm-target="#confirmAllowPublicAccessModal">Allow public access</button>""", body);
        Assert.Contains("Are you sure you want to make this profile version public (this cannot be undone)?", body);
        Assert.Contains("handler=AllowPublicAccess", body);
        Assert.DoesNotContain("/SurveillanceProfiles/AllowPublicAccess", body);
        Assert.Null(apiClient.PublicAccessProfileVersionId);
    }

    [Fact]
    public async Task ManageProfile_OffersNoAllowPublicAccess_WhenTheProfileDoesNotAllowIt()
    {
        using var factory = CreateFactory(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: Profile(canAllowPublicAccess: false)));
        var client = await SignedInClientWithoutRedirectsAsync(factory);

        var body = await client.GetStringAsync($"/SurveillanceProfiles/ManageProfile/{ProfileId}");

        Assert.DoesNotContain("Allow public access", body);
        Assert.DoesNotContain("confirmAllowPublicAccessModal", body);
    }

    [Fact]
    public async Task AllowPublicAccess_MakesTheCurrentPublishedVersionPublic_AndStaysOnTheManageProfilePage()
    {
        var apiClient = new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: Profile(canAllowPublicAccess: true));
        using var factory = CreateFactory(apiClient);
        var client = await SignedInClientWithoutRedirectsAsync(factory);

        var response = await PostAllowPublicAccessAsync(client);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(PublishedVersionId, apiClient.PublicAccessProfileVersionId);
        Assert.Contains("Manage profile:", body);
        Assert.Contains("govuk-notification-banner--success", body);
        Assert.Contains("Successfully made the latest published version public.", body);
    }

    [Fact]
    public async Task AllowPublicAccess_ShowsTheErrorOnTheManageProfilePage_WhenTheVersionCannotBeMadePublic()
    {
        using var factory = CreateFactory(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: Profile(canAllowPublicAccess: true), setProfileVersionPublicAccessResult: false));
        var client = await SignedInClientWithoutRedirectsAsync(factory);

        var response = await PostAllowPublicAccessAsync(client);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("This profile version could not be made public. Please try again.", body);
        Assert.DoesNotContain("Successfully made", body);
    }

    private static ManageProfileViewModel Profile(bool canAllowPublicAccess) => new()
    {
        ProfileId = ProfileId,
        ProfileTitle = "Anthrax",
        LatestPublishedVersionDefraNetOnly = "Version 14.0",
        CurrentProfileVersionId = PublishedVersionId,
        CurrentPublishedVersionId = PublishedVersionId,
        LatestVersionId = PublishedVersionId,
        LinkVisibility = new ManageProfileLinkVisibilityDto { CanAllowPublicAccess = canAllowPublicAccess }
    };

    private static async Task<HttpClient> SignedInClientWithoutRedirectsAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await client.GetAsync(CdcWebTestFactory.TestSignInPath);

        return client;
    }

    private static async Task<HttpResponseMessage> PostAllowPublicAccessAsync(HttpClient client)
    {
        var page = await client.GetStringAsync($"/SurveillanceProfiles/ManageProfile/{ProfileId}");
        var token = Regex.Match(page, """name="__RequestVerificationToken"[^>]*value="([^"]+)""", RegexOptions.None, TimeSpan.FromSeconds(5));
        Assert.True(token.Success, "No antiforgery token found on the Manage profile page.");

        return await client.PostAsync(
            $"/SurveillanceProfiles/ManageProfile/{ProfileId}?handler=AllowPublicAccess",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token.Groups[1].Value,
                ["ProfileId"] = ProfileId.ToString()
            }));
    }

    private static WebApplicationFactory<Program> CreateFactory(CDC.Web.Tests.Features.Landing.FakeApiClient apiClient) =>
        new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(apiClient);
            }));
}
