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
        using var factory = CreateFactory(new PublishProfileResult(PublishProfileOutcome.Success, NewVersionId, null));
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

    [Theory]
    [InlineData("You cannot publish a profile based on an already published version.")]
    [InlineData("You cannot publish this scenario because the profile to which it belongs is not published.")]
    [InlineData("You cannot make this scenario public because there is no public version of this profile.")]
    public async Task Publish_ShowsTheErrorOnTheManageProfilePage_WhenThePublishFails(string message)
    {
        using var factory = CreateFactory(new PublishProfileResult(PublishProfileOutcome.Conflict, null, message));
        var client = await SignedInClientWithoutRedirectsAsync(factory);

        var response = await PostPublishAsync(client, "PublishPublic");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(message, body);
        Assert.DoesNotContain("Successfully published", body);
        Assert.DoesNotContain("validation error", body);
    }

    [Theory]
    [InlineData("PublishPublic")]
    [InlineData("PublishDefranetOnly")]
    public async Task Publish_ListsTheValidationErrorsOnTheManageProfilePage_AndDoesNotPublish_WhenTheDraftIsInvalid(string handler)
    {
        ProfileSectionInvalidReasonDto[] reasons =
        [
            new() { SectionNumber = 1, QuestionNumber = 0, Description = "\"Revision dates, Next policy review\" is required" },
            new() { SectionNumber = 3, QuestionNumber = 2, QuestionId = Guid.NewGuid(), Description = "Cattle Row 2, \"Laboratory test - Name\" is required" }
        ];
        using var factory = CreateFactory(new PublishProfileResult(PublishProfileOutcome.InvalidProfile, null, null) { InvalidReasons = reasons });
        var client = await SignedInClientWithoutRedirectsAsync(factory);

        var response = await PostPublishAsync(client, handler);
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-error-summary", body);
        Assert.Contains("There are 2 validation errors", body);
        Assert.Contains("<li>1.0 \"Revision dates, Next policy review\" is required</li>", body);
        Assert.Contains("<li>3.2 Cattle Row 2, \"Laboratory test - Name\" is required</li>", body);
        Assert.Contains("Error: Manage profile: Anthrax", body);
        Assert.DoesNotContain("Successfully published", body);
    }

    [Fact]
    public async Task Publish_UsesTheSingularHeading_WhenTheDraftHasOneValidationError()
    {
        ProfileSectionInvalidReasonDto[] reasons = [new() { SectionNumber = 2, QuestionNumber = 1, Description = "\"Is it endemic? - Endemic\" is required" }];
        using var factory = CreateFactory(new PublishProfileResult(PublishProfileOutcome.InvalidProfile, null, null) { InvalidReasons = reasons });
        var client = await SignedInClientWithoutRedirectsAsync(factory);

        var response = await PostPublishAsync(client, "PublishPublic");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("There is 1 validation error", body);
        Assert.DoesNotContain("validation errors", body);
    }

    [Fact]
    public async Task ManageProfile_ShowsNoValidationErrors_WhenItIsOnlyViewed()
    {
        using var factory = CreateFactory(new PublishProfileResult(PublishProfileOutcome.Success, NewVersionId, null));
        var client = await SignedInClientWithoutRedirectsAsync(factory);

        var body = await client.GetStringAsync($"/SurveillanceProfiles/ManageProfile/{ProfileId}");

        Assert.DoesNotContain("validation error", body);
        Assert.DoesNotContain("Error: Manage profile", body);
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

    private static WebApplicationFactory<Program> CreateFactory(PublishProfileResult publishResult)
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
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile, publishProfileResult: publishResult));
                services.RemoveAll<IProfileReportsApiService>();
                services.AddSingleton<IProfileReportsApiService>(new FakeProfileReportsApiService(overview: new ProfileReportsOverviewResult(ProfileReportsOutcome.Success, overview)));
            }));
    }
}
