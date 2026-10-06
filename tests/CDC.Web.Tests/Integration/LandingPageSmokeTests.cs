using System.Net;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CDC.Web.Tests.Integration;

// AddCidmAuthentication() requires Cidm config to be present for host startup validation, so this
// uses CdcWebTestFactory (not the plain WebApplicationFactory<Program>) even for routes unrelated to auth.
public class LandingPageSmokeTests : IClassFixture<CdcWebTestFactory>
{
    private readonly CdcWebTestFactory _factory;

    public LandingPageSmokeTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Landing/Error")]
    [InlineData("/HelpSupport/QualityStatement")]
    public async Task LandingRoutes_ReturnSuccess(string url)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Program.cs only registers the global exception handler middleware outside Development, so a
    // Development-environment test host (the default for every other test here) never exercises
    // that branch. A dedicated Production-environment host proves the app still starts and serves
    // requests with it registered.
    [Fact]
    public async Task LandingRoutes_ReturnSuccess_InProductionEnvironment()
    {
        await using var productionFactory = _factory.WithWebHostBuilder(builder => builder
            .UseEnvironment("Production")
            .UseSetting("Api:BaseUrl", "http://cdc-api.test"));
        var client = productionFactory.CreateClient();

        var response = await client.GetAsync("/Landing/Error");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The other ManageProfile smoke tests only exercise the error-state view (the real ApiClient
    // has no live CDC.Api to call in tests), so this fakes a successful response to render the
    // page's happy-path markup too.
    [Fact]
    public async Task ManageProfile_WithAProfile_RendersTheHappyPathView()
    {
        var profileId = Guid.NewGuid();
        await using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddScoped<IApiClient>(_ => new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: new ManageProfileViewModel
            {
                ProfileId = profileId,
                ProfileTitle = "Bovine Tuberculosis",
                ScenarioTitle = "Default Scenario",
                LatestPublishedVersionPublic = "Version 5",
                LatestPublishedVersionDefraNetOnly = "Version 7",
                LatestDraftVersion = "Version 8",
                ProfileStatus = "Draft"
            }))));
        var client = factory.CreateClient();
        var signIn = await client.GetAsync(CdcWebTestFactory.TestSignInPath);
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);

        var response = await client.GetAsync($"/SurveillanceProfiles/ManageProfile/{profileId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Bovine Tuberculosis", await response.Content.ReadAsStringAsync());
    }

    // The search filters are resubmitted via a background fetch that only wants the results
    // fragment re-rendered, not a full page reload - signalled by this header.
    [Fact]
    public async Task Search_WithAjaxHeader_ReturnsOnlyTheResultsPartial()
    {
        var client = _factory.CreateClient();
        var signIn = await client.GetAsync(CdcWebTestFactory.TestSignInPath);
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/SurveillanceProfiles/Search");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

// These pages are business/admin functionality with no anonymous exemption - the app's default
// authorization policy (RequireAuthenticatedUser) must redirect anonymous requests to Entra ID
// (the app-wide fallback scheme) rather than rendering them.
public class ProtectedPagesSmokeTests : IClassFixture<CdcWebTestFactory>
{
    private readonly CdcWebTestFactory _factory;

    public ProtectedPagesSmokeTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/Landing/Internal")]
    [InlineData("/SurveillanceProfiles/Search")]
    [InlineData("/DiseaseProfiles/Create")]
    [InlineData("/DiseaseProfiles/CompareVersions")]
    [InlineData("/DiseaseProfiles/ReviewTimings")]
    [InlineData("/Reports/General")]
    [InlineData("/Reports/QuestionsGuidance")]
    [InlineData("/Reports/DiseaseRanking")]
    [InlineData("/SpeciesData/Maintain")]
    [InlineData("/ViewSpeciesData")]
    [InlineData("/CrossProfileAdmin/CrossCuttingIssueScores")]
    [InlineData("/CrossProfileAdmin/PrioritisationVariables")]
    [InlineData("/CrossProfileAdmin/ReferenceData")]
    [InlineData("/HelpSupport/StaticReports")]
    [InlineData("/HelpSupport/StaticReports?UserManual=1")]
    [InlineData("/UserAdmin/ExternalUsers")]
    [InlineData("/UserAdmin/GlobalUsers")]
    [InlineData("/SurveillanceProfiles/PublishPublic/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f")]
    [InlineData("/SurveillanceProfiles/PublishDefranetOnly/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f")]
    [InlineData("/SurveillanceProfiles/ViewContributionsReport/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f")]
    [InlineData("/SurveillanceProfiles/CloneNewScenario/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f")]
    [InlineData("/SurveillanceProfiles/AllowPublicAccess/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f")]
    [InlineData("/SurveillanceProfiles/CreateNewDraftVersion/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f")]
    [InlineData("/SurveillanceProfiles/MaintainContributorsAndReviewers/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f")]
    [InlineData("/SurveillanceProfiles/EditProperties/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f")]
    [InlineData("/SurveillanceProfiles/ManageProfile/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f")]
    [InlineData("/SurveillanceProfiles/EditProfileTitle/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f")]
    public async Task ProtectedRoutes_Anonymous_RedirectToEntraRatherThanRenderingView(string url)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(CdcWebTestFactory.FakeEntraAuthorizationEndpoint, response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("/Landing/Internal")]
    [InlineData("/SurveillanceProfiles/Search")]
    [InlineData("/DiseaseProfiles/Create")]
    [InlineData("/DiseaseProfiles/CompareVersions")]
    [InlineData("/DiseaseProfiles/ReviewTimings")]
    [InlineData("/Reports/General")]
    [InlineData("/Reports/QuestionsGuidance")]
    [InlineData("/Reports/DiseaseRanking")]
    [InlineData("/SpeciesData/Maintain")]
    [InlineData("/ViewSpeciesData")]
    [InlineData("/CrossProfileAdmin/CrossCuttingIssueScores")]
    [InlineData("/CrossProfileAdmin/PrioritisationVariables")]
    [InlineData("/CrossProfileAdmin/ReferenceData")]
    [InlineData("/HelpSupport/StaticReports")]
    [InlineData("/HelpSupport/StaticReports?UserManual=1")]
    [InlineData("/UserAdmin/ExternalUsers")]
    [InlineData("/UserAdmin/GlobalUsers")]
    [InlineData("/SurveillanceProfiles/PublishPublic/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f")]
    [InlineData("/SurveillanceProfiles/PublishDefranetOnly/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f")]
    [InlineData("/SurveillanceProfiles/ViewContributionsReport/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f")]
    [InlineData("/SurveillanceProfiles/CloneNewScenario/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f")]
    [InlineData("/SurveillanceProfiles/AllowPublicAccess/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f")]
    [InlineData("/SurveillanceProfiles/CreateNewDraftVersion/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f")]
    [InlineData("/SurveillanceProfiles/MaintainContributorsAndReviewers/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f")]
    [InlineData("/SurveillanceProfiles/EditProperties/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f")]
    [InlineData("/SurveillanceProfiles/ManageProfile/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f")]
    [InlineData("/SurveillanceProfiles/EditProfileTitle/6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f")]
    public async Task ProtectedRoutes_AuthenticatedSession_ReturnSuccess(string url)
    {
        var client = _factory.CreateClient();

        var signIn = await client.GetAsync(CdcWebTestFactory.TestSignInPath);
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

// LandingController.External() now requires authentication - an anonymous request must be
// redirected to CIDM (the default Cookie scheme forwards the challenge to the "cidm" scheme), not
// rendered directly. Uses CdcWebTestFactory (not the plain WebApplicationFactory<Program> above)
// because AddCidmAuthentication() would otherwise attempt a real network discovery call at startup.
public class LandingExternalAuthSmokeTests : IClassFixture<CdcWebTestFactory>
{
    private readonly CdcWebTestFactory _factory;

    public LandingExternalAuthSmokeTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task External_Anonymous_RedirectsToCidmRatherThanRenderingView()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Landing/External");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(CdcWebTestFactory.FakeAuthorizationEndpoint, response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task External_AuthenticatedSession_RendersSignedInView()
    {
        var client = _factory.CreateClient();

        var signIn = await client.GetAsync(CdcWebTestFactory.TestSignInPath);
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);

        var response = await client.GetAsync("/Landing/External");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

// LandingController.Internal() requires authentication via the app's fallback policy, which
// resolves to Entra ID's cookie scheme (this app is predominantly used by internal staff).
public class LandingInternalAuthSmokeTests : IClassFixture<CdcWebTestFactory>
{
    private readonly CdcWebTestFactory _factory;

    public LandingInternalAuthSmokeTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Internal_Anonymous_RedirectsToEntraRatherThanRenderingView()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/Landing/Internal");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith(CdcWebTestFactory.FakeEntraAuthorizationEndpoint, response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Internal_AuthenticatedSession_RendersSignedInView()
    {
        var client = _factory.CreateClient();

        var signIn = await client.GetAsync(CdcWebTestFactory.TestSignInPath);
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);

        var response = await client.GetAsync("/Landing/Internal");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

