using System.Net;
using CDC.Web.Authorization.Constants;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Features.Landing;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// A header menu or footer link is only a convenience: each page it leads to must refuse a signed-in user whose role the
// link's policy does not allow, so typing the address gets the same answer as the link.
public class MenuPageAuthorizationIntegrationTests : IClassFixture<CdcWebTestFactory>
{
    private const string RefusalHeading = "You do not have permission to view this page";

    private readonly CdcWebTestFactory _factory;

    public MenuPageAuthorizationIntegrationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void EveryHeaderMenuPage_EnforcesTheSamePolicyAsItsLink() =>
        AssertEveryPageEnforcesItsLinkPolicy(HeaderMenu.Sections.SelectMany(section => section.Links).Select(link => (link.Text, link.PagePath, link.RequiredPolicy)));

    [Fact]
    public void EveryFooterPage_EnforcesTheSamePolicyAsItsLink() =>
        AssertEveryPageEnforcesItsLinkPolicy(FooterMenu.Sections.SelectMany(section => section.Links).Select(link => (link.Text, link.PagePath, link.RequiredPolicy)));

    private void AssertEveryPageEnforcesItsLinkPolicy(IEnumerable<(string Text, string PagePath, string RequiredPolicy)> links)
    {
        var endpoints = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints;

        foreach (var link in links)
        {
            var pageEndpoints = endpoints
                .Where(endpoint => endpoint.Metadata.GetMetadata<PageActionDescriptor>()?.ViewEnginePath == link.PagePath)
                .ToList();

            Assert.True(pageEndpoints.Count > 0, $"No Razor page was found for the '{link.Text}' link ({link.PagePath}).");
            Assert.All(
                pageEndpoints,
                endpoint => Assert.Contains(
                    endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(),
                    authorizeData => authorizeData.Policy == link.RequiredPolicy));
        }
    }

    // Pages a Policy Profile user's role does not include.
    [Theory]
    [InlineData("/SurveillanceProfiles/ProfileProperties")]
    [InlineData("/DiseaseProfiles/CompareVersions")]
    [InlineData("/DiseaseProfiles/ReviewTimings")]
    [InlineData("/Reports/QuestionsGuidance")]
    [InlineData("/SpeciesData/Maintain")]
    [InlineData("/UserAdmin/GlobalUsers")]
    [InlineData("/UserAdmin/ExternalUsers")]
    [InlineData("/CrossProfileAdmin/ReferenceData")]
    [InlineData("/CrossProfileAdmin/PrioritisationVariables")]
    [InlineData("/CrossProfileAdmin/CrossCuttingIssueScores")]
    public async Task PolicyProfileUser_IsRefusedPagesTheirRoleDoesNotInclude(string path)
    {
        var client = await _factory.SignedInClientWithPermissionsAsync(isProfileEditor: false, isPolicyProfileUser: true);

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(RefusalHeading, await response.Content.ReadAsStringAsync());
    }

    // Help and Support is open to every signed-in user: the footer shows it to a user whose record holds no permission, and the pages let them in.
    [Theory]
    [InlineData("/HelpSupport/StaticReports")]
    [InlineData("/HelpSupport/QualityStatement")]
    public async Task UserWithNoPermissions_IsAllowedTheHelpAndSupportPages(string path)
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IApiClient>();
            services.AddSingleton<IApiClient>(new FakeApiClient());
        }));
        var client = await factory.SignedInClientWithPermissionsAsync(isProfileEditor: false, isPolicyProfileUser: false);

        var response = await client.GetAsync(path);

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.DoesNotContain(RefusalHeading, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ProfileEditor_IsAllowedAnEditorOnlyPage()
    {
        var client = await _factory.SignedInClientWithPermissionsAsync(isProfileEditor: true, isPolicyProfileUser: false);

        var response = await client.GetAsync("/Reports/QuestionsGuidance");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Reviewer_IsAllowedReviewTimings_ButRefusedAnEditorOnlyPage()
    {
        var client = await _factory.SignedInClientWithRolesAsync(AuthorizationRoles.Reviewer);

        var allowed = await client.GetAsync("/DiseaseProfiles/ReviewTimings");
        var refused = await client.GetAsync("/Reports/QuestionsGuidance");

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
    }
}
