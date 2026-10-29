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

// Drives the real pipeline from the Manage profile delete dialog to the home page the legacy page redirected to.
public class ManageProfileDeleteIntegrationTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();

    // An external user cannot reach Manage profile yet (its pages challenge for Entra ID), so that branch is covered by ManageProfileModelTests.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeleteCurrentVersion_RedirectsAnInternalUserToTheInternalHomePage(bool isProfileDeleted)
    {
        using var factory = CreateFactory(new DeleteProfileVersionResult(DeleteProfileVersionOutcome.Success, isProfileDeleted, null));
        var client = await SignedInClientWithoutRedirectsAsync(factory);

        var response = await PostDeleteAsync(client);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Landing/Internal", response.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(response.Headers.Location)).StatusCode);
    }

    [Fact]
    public async Task DeleteCurrentVersion_StaysOnManageProfileWithTheError_WhenTheDeletionFails()
    {
        const string message = "This profile version could not be found.";
        using var factory = CreateFactory(new DeleteProfileVersionResult(DeleteProfileVersionOutcome.NotFound, false, message));
        var client = await SignedInClientWithoutRedirectsAsync(factory);

        var response = await PostDeleteAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(message, await response.Content.ReadAsStringAsync());
    }

    private static async Task<HttpClient> SignedInClientWithoutRedirectsAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await client.GetAsync(CdcWebTestFactory.TestSignInPath);

        return client;
    }

    private static async Task<HttpResponseMessage> PostDeleteAsync(HttpClient client)
    {
        var page = await client.GetStringAsync($"/SurveillanceProfiles/ManageProfile/{ProfileId}");
        var token = Regex.Match(page, """name="__RequestVerificationToken"[^>]*value="([^"]+)""", RegexOptions.None, TimeSpan.FromSeconds(5));
        Assert.True(token.Success, "No antiforgery token found on the Manage profile page.");

        return await client.PostAsync(
            $"/SurveillanceProfiles/ManageProfile/{ProfileId}?handler=DeleteCurrentVersion",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token.Groups[1].Value,
                ["ProfileId"] = ProfileId.ToString()
            }));
    }

    private static WebApplicationFactory<Program> CreateFactory(DeleteProfileVersionResult deleteResult)
    {
        var profile = new ManageProfileViewModel
        {
            ProfileId = ProfileId,
            ProfileTitle = "Anthrax",
            CurrentProfileVersionId = Guid.NewGuid(),
            LatestVersionId = Guid.NewGuid(),
            LinkVisibility = new ManageProfileLinkVisibilityDto { CanDeleteCurrentVersion = true }
        };

        return new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile, deleteProfileVersionResult: deleteResult));
            }));
    }
}
