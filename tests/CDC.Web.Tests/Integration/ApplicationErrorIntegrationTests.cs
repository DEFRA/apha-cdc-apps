using System.Net;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// A user CDC.Api refuses a profile is sent to the Application error page, as the legacy site's SecurityException sent them to its own.
public class ApplicationErrorIntegrationTests(CdcWebTestFactory factory) : IClassFixture<CdcWebTestFactory>
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid SectionId = Guid.NewGuid();
    private static readonly Guid DraftVersionId = Guid.NewGuid();

    // The search page lists every draft, so the link of a draft names its version: CDC.Api refuses it to a user with no role on the
    // profile, while the link of the published card, which names none the user can be refused, still shows the published profile.
    [Fact]
    public async Task BrowseProfile_SendsAnExternalUserToTheApplicationErrorPage_WhenTheyFollowADraftsLink()
    {
        using var refusing = factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(
                    manageProfile: new ManageProfileViewModel { ProfileId = ProfileId, ProfileTitle = "Bovine tuberculosis", CurrentProfileVersionId = Guid.NewGuid() },
                    throwOnGetManageProfileForAVersion: ProfileTabTestData.Refused()));
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(new FakeProfileSectionsApiService());
            }));
        var client = await refusing.SignedInExternalClientAsync();

        var draftsLink = await client.GetAsync($"/SurveillanceProfiles/EditProfileQuestions/{ProfileId}?profileVersionId={DraftVersionId}");
        var withoutVersion = await client.GetAsync($"/SurveillanceProfiles/EditProfileQuestions/{ProfileId}");

        Assert.Equal(HttpStatusCode.Redirect, draftsLink.StatusCode);
        Assert.Equal(ApplicationErrorModel.PagePath, draftsLink.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, withoutVersion.StatusCode);
    }

    [Fact]
    public async Task BrowseProfile_SendsAnExternalUserCdcApiRefuses_ToTheApplicationErrorPage()
    {
        using var refusing = WithCdcApiRefusingTheProfile();
        var client = await refusing.SignedInExternalClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/EditProfileQuestions/{ProfileId}?section={SectionId}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(ApplicationErrorModel.PagePath, response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task ApplicationError_ShowsTheTechnicalDetailsOfTheRefusalOnce()
    {
        using var refusing = WithCdcApiRefusingTheProfile();
        var client = await refusing.SignedInExternalClientAsync();
        await client.GetAsync($"/SurveillanceProfiles/EditProfileQuestions/{ProfileId}?section={SectionId}");

        var shown = await client.GetAsync(ApplicationErrorModel.PagePath);
        var body = await shown.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, shown.StatusCode);
        Assert.Contains("<h1 class=\"govuk-heading-xl\">Application error</h1>", body);
        Assert.Contains("An error has occurred. Please contact the system administrator if this problem persists.", body);
        Assert.Contains("Technical details", body);
        Assert.Contains(ProfileSectionTabPageModel.AccessRefusedErrorType, body);
        Assert.Contains(ProfileSectionTabPageModel.AccessRefusedDetails, body);
        Assert.Contains($"https://localhost/SurveillanceProfiles/EditProfileQuestions/{ProfileId}?section={SectionId}", body);
        Assert.Contains("Time of error", body);
        Assert.Contains("Search disease profiles", body);
        Assert.Contains("aria-current=\"page\">Browse profile</li>", body);

        var refreshed = await (await client.GetAsync(ApplicationErrorModel.PagePath)).Content.ReadAsStringAsync();

        Assert.Contains("Application error", refreshed);
        Assert.DoesNotContain("Technical details", refreshed);
    }

    [Fact]
    public async Task ApplicationError_ShowsNoTechnicalDetails_WhenNoPageSentTheUserThere()
    {
        var client = await factory.SignedInExternalClientAsync();

        var response = await client.GetAsync(ApplicationErrorModel.PagePath);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Application error", body);
        Assert.DoesNotContain("Technical details", body);
    }

    private WebApplicationFactory<Program> WithCdcApiRefusingTheProfile() =>
        factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(throwOnGetManageProfile: ProfileTabTestData.Refused()));
            }));
}
