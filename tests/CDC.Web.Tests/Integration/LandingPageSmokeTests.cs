using System.Net;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

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
    [InlineData("/HelpSupport/HelpUsingD2R2")]
    [InlineData("/HelpSupport/QualityStatement")]
    [InlineData("/UserAdmin/ExternalUsers")]
    [InlineData("/UserAdmin/GlobalUsers")]
    public async Task LandingRoutes_ReturnSuccess(string url)
    {
        var client = _factory.CreateClient();

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

