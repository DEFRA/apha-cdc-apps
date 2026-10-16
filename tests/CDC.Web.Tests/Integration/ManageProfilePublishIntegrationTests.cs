using System.Net;
using System.Text.RegularExpressions;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// Drives the real pipeline from the Manage profile publish dialog to the Profile reports page the legacy page redirected to.
public class ManageProfilePublishIntegrationTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid NewVersionId = Guid.NewGuid();

    [Theory]
    [InlineData("PublishPublic", "Successfully published this profile and made it public.")]
    [InlineData("PublishDefranetOnly", "Successfully published this profile but did not make it public.")]
    public async Task Publish_RedirectsToTheNewVersionsProfileReports_WhichShowsTheSuccessMessageOnce(string handler, string message)
    {
        using var factory = CreateFactory(new CreateNewProfileVersionResult(CreateNewProfileVersionOutcome.Success, NewVersionId, null));
        var client = await SignedInClientWithoutRedirectsAsync(factory);

        var response = await PostPublishAsync(client, handler);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal($"/SurveillanceProfiles/ProfileReports/{NewVersionId}", response.Headers.Location?.OriginalString);
        var reports = await client.GetStringAsync(response.Headers.Location);
        Assert.Contains("Profile reports for Anthrax v2.0", reports);
        Assert.Contains("govuk-notification-banner--success", reports);
        Assert.Contains(message, reports);
        Assert.DoesNotContain(message, await client.GetStringAsync(response.Headers.Location));
    }

    [Fact]
    public async Task Publish_ShowsTheErrorOnTheManageProfilePage_WhenThePublishFails()
    {
        using var factory = CreateFactory(new CreateNewProfileVersionResult(CreateNewProfileVersionOutcome.Conflict, null, "You cannot publish a profile based on an already published version."));
        var client = await SignedInClientWithoutRedirectsAsync(factory);

        var response = await PostPublishAsync(client, "PublishPublic");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("You cannot publish a profile based on an already published version.", body);
        Assert.DoesNotContain("Successfully published", body);
    }

    private static async Task<HttpClient> SignedInClientWithoutRedirectsAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await client.GetAsync(CdcWebTestFactory.TestSignInPath);

        return client;
    }

    private static async Task<HttpResponseMessage> PostPublishAsync(HttpClient client, string handler)
    {
        var page = await client.GetStringAsync($"/SurveillanceProfiles/ManageProfile/{ProfileId}");
        var token = Regex.Match(page, """name="__RequestVerificationToken"[^>]*value="([^"]+)""", RegexOptions.None, TimeSpan.FromSeconds(5));
        Assert.True(token.Success, "No antiforgery token found on the Manage profile page.");

        return await client.PostAsync(
            $"/SurveillanceProfiles/ManageProfile/{ProfileId}?handler={handler}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token.Groups[1].Value,
                ["ProfileId"] = ProfileId.ToString()
            }));
    }

    private static WebApplicationFactory<Program> CreateFactory(CreateNewProfileVersionResult publishResult)
    {
        var profile = new ManageProfileViewModel
        {
            ProfileId = ProfileId,
            ProfileTitle = "Anthrax",
            CurrentProfileVersionId = Guid.NewGuid(),
            LatestVersionId = Guid.NewGuid(),
            LinkVisibility = new ManageProfileLinkVisibilityDto { CanPublishPublic = true, CanPublishDefranetOnly = true }
        };
        var overview = new ProfileReportsOverviewDto { ProfileVersionId = NewVersionId, ProfileId = ProfileId, FullTitle = "Anthrax", VersionLabel = "2.0", IsCurrent = true };

        return new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile, createNewProfileVersionResult: publishResult));
                services.RemoveAll<IProfileReportsApiService>();
                services.AddSingleton<IProfileReportsApiService>(new FakeProfileReportsApiService(overview: new ProfileReportsOverviewResult(ProfileReportsOutcome.Success, overview)));
            }));
    }
}
