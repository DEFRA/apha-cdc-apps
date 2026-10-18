using System.Net;
using System.Text.RegularExpressions;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// Drives the real pipeline - routing, authorization, model binding, antiforgery and the Razor view - for the
// Profile properties page, with only the CDC.Api clients replaced.
public partial class ProfilePropertiesIntegrationTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid DraftVersionId = Guid.NewGuid();
    private static readonly Guid CloneVersionId = Guid.NewGuid();
    private static readonly Guid Animals = Guid.NewGuid();
    private static readonly Guid Cattle = Guid.NewGuid();
    private static readonly Guid Sheep = Guid.NewGuid();
    private static readonly Guid Goats = Guid.NewGuid();

    private static readonly SpeciesDto[] Species =
    [
        new() { Id = Animals, ParentId = Guid.Empty, Description = "Animals", IsActive = true },
        new() { Id = Cattle, ParentId = Animals, Description = "Cattle", IsActive = true },
        new() { Id = Sheep, ParentId = Animals, Description = "Sheep", IsActive = true },
        new() { Id = Goats, ParentId = Animals, Description = "Goats", IsActive = false }
    ];

    private static ProfileAttributesDto Profile() => new()
    {
        Id = ProfileId,
        Title = "Bovine <em>tuberculosis</em>",
        CurrentDraftProfileVersionId = DraftVersionId,
        LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8],
        AffectedSpecies =
        [
            new AffectedSpeciesDto { SpeciesId = Cattle, Name = "Cattle", Type = "Profiled", IsActive = true },
            new AffectedSpeciesDto { SpeciesId = Goats, Name = "Goats", Type = "Other", IsActive = false }
        ]
    };

    [Fact]
    public async Task EditPage_ShowsTheProfilesTitleAndSpecies_AndLinksBackToManageProfile()
    {
        using var factory = CreateFactory(new FakeProfilePropertiesApiService());
        var client = await factory.SignedInClientAsync();

        var page = await client.GetStringAsync($"/SurveillanceProfiles/ProfileProperties/{ProfileId}");

        Assert.Contains("Profile properties for Bovine <em>tuberculosis</em>", page);
        Assert.Contains("Update</button>", page);
        Assert.Contains("Reset selections</button>", page);
        Assert.Contains($"href=\"/SurveillanceProfiles/ManageProfile/{ProfileId}\"", page);
        Assert.Contains("Bovine <em>tuberculosis</em></div>", page);
        Assert.Contains("Profiled species", page);
        Assert.Contains("Other affected species", page);
        Assert.Contains("Removing profiled species from a profile will remove all of the data associated with that species", page);
        Assert.DoesNotContain("Use this function to create a brand new Surveillance Profile", page);
        Assert.Matches(CheckedBoxRegex("profiled-species", Cattle), page);
        Assert.Matches(CheckedBoxRegex("other-species", Goats), page);
        Assert.DoesNotMatch(CheckedBoxRegex("profiled-species", Sheep), page);
        Assert.Contains("(inactive)", page);
        // The header's Create profile link must not pick up this page's own profile id.
        Assert.Contains("href=\"/SurveillanceProfiles/ProfileProperties\"", page);
    }

    [Fact]
    public async Task CreatePage_ShowsTheIntroduction_AndTheHeaderMenuLinksToIt()
    {
        using var factory = CreateFactory(new FakeProfilePropertiesApiService());
        var client = await factory.SignedInClientAsync();

        var page = await client.GetStringAsync("/SurveillanceProfiles/ProfileProperties");

        Assert.Contains("Create new profile", page);
        Assert.Contains("Use this function to create a brand new Surveillance Profile", page);
        Assert.Contains("Create profile</button>", page);
        Assert.DoesNotContain("Removing profiled species from a profile", page);
        Assert.DoesNotContain("Manage profile</a>", page);
        Assert.Contains("href=\"/SurveillanceProfiles/ProfileProperties\"", page);
    }

    [Fact]
    public async Task ClonePage_ShowsWhereTheProfileIsClonedFrom()
    {
        var properties = new FakeProfilePropertiesApiService(defaults: new NewProfileDefaultsDto
        {
            Title = "Anthrax",
            ClonedVersionDescription = "<em>Anthrax</em> - v2.0 (Published) Valid from 01/01/2020",
            AffectedSpecies = [new AffectedSpeciesDto { SpeciesId = Sheep, Name = "Sheep", Type = "Profiled", IsActive = true }]
        });
        using var factory = CreateFactory(properties);
        var client = await factory.SignedInClientAsync();

        var page = await client.GetStringAsync($"/SurveillanceProfiles/ProfileProperties?cloneProfileVersionId={CloneVersionId}");

        Assert.Equal((CloneVersionId, false), properties.LastDefaultsRequest);
        Assert.Contains("Clone new profile", page);
        Assert.Contains("The new profile will be cloned from: <em>Anthrax</em> - v2.0 (Published) Valid from 01/01/2020", page);
        Assert.Matches(CheckedBoxRegex("profiled-species", Sheep), page);
    }

    [Fact]
    public async Task ManageProfile_LinksToTheProfilePropertiesPage()
    {
        using var factory = CreateFactory(new FakeProfilePropertiesApiService());
        var client = await factory.SignedInClientAsync();

        var page = await client.GetStringAsync($"/SurveillanceProfiles/ManageProfile/{ProfileId}");

        Assert.Contains($"href=\"/SurveillanceProfiles/ProfileProperties/{ProfileId}\">Edit properties</a>", page);
        Assert.Contains($"href=\"/SurveillanceProfiles/ProfileProperties?cloneProfileVersionId={CloneVersionId}\">Clone new profile</a>", page);
        Assert.Contains($"href=\"/SurveillanceProfiles/ProfileProperties?cloneProfileVersionId={CloneVersionId}&amp;scenario=true\">Clone new scenario</a>", page);
    }

    [Fact]
    public async Task Create_RedirectsToTheNewProfilesQuestions_WithTheSpeciesTheUserTicked()
    {
        var properties = new FakeProfilePropertiesApiService();
        using var factory = CreateFactory(properties);
        var client = await SignedInClientWithoutRedirectsAsync(factory);

        var response = await PostAsync(client, "/SurveillanceProfiles/ProfileProperties", new Dictionary<string, string[]>
        {
            ["ProfileTitle"] = ["Anthrax"],
            ["ProfiledSpeciesIds"] = [Cattle.ToString()],
            ["OtherSpeciesIds"] = [Sheep.ToString()]
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var request = properties.LastCreateRequest!;
        Assert.Equal($"/SurveillanceProfiles/EditProfileQuestions/{request.Id}", response.Headers.Location?.OriginalString);
        Assert.Equal("Anthrax", request.Title);
        Assert.Equal(["Profiled", "Other"], request.AffectedSpeciesInsertList.Select(species => species.Type));
    }

    [Fact]
    public async Task Create_ShowsTheErrorsInASummaryThatLinksToTheFields_AndKeepsWhatWasPicked()
    {
        var properties = new FakeProfilePropertiesApiService();
        using var factory = CreateFactory(properties);
        var client = await SignedInClientWithoutRedirectsAsync(factory);

        var response = await PostAsync(client, "/SurveillanceProfiles/ProfileProperties", new Dictionary<string, string[]>
        {
            ["ProfileTitle"] = [""],
            ["OtherSpeciesIds"] = [Sheep.ToString()]
        });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(properties.LastCreateRequest);
        Assert.Contains("There is a problem", body);
        Assert.Contains("<a href=\"#ProfileTitleEditor\">You must enter a profile title</a>", body);
        Assert.Contains("<a href=\"#profiled-species-group\">You must specify at least one profiled species</a>", body);
        Assert.Contains("<title>Error: Create new profile", body);
        Assert.Matches(CheckedBoxRegex("other-species", Sheep), body);
    }

    [Fact]
    public async Task Update_SavesThenShowsTheSuccessMessageOnce()
    {
        var properties = new FakeProfilePropertiesApiService();
        using var factory = CreateFactory(properties);
        var client = await SignedInClientWithoutRedirectsAsync(factory);
        var url = $"/SurveillanceProfiles/ProfileProperties/{ProfileId}";

        var response = await PostAsync(client, url, new Dictionary<string, string[]>
        {
            ["ProfileTitle"] = ["Bovine tuberculosis"],
            ["LastUpdated"] = [Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8])],
            ["ProfiledSpeciesIds"] = [Sheep.ToString()],
            ["OtherSpeciesIds"] = [Goats.ToString()]
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(url, response.Headers.Location?.OriginalString);
        var update = properties.LastUpdate!.Value.Request;
        Assert.Equal("Bovine tuberculosis", update.Title);
        Assert.Equal(Cattle, Assert.Single(update.AffectedSpeciesDeleteList).SpeciesId);
        Assert.Equal(Sheep, Assert.Single(update.AffectedSpeciesInsertList).SpeciesId);

        var reloaded = await client.GetStringAsync(url);
        Assert.Contains("govuk-notification-banner--success", reloaded);
        Assert.Contains("Your changes were successfully saved", reloaded);
        Assert.DoesNotContain("Your changes were successfully saved", await client.GetStringAsync(url));
    }

    private static Regex CheckedBoxRegex(string treePrefix, Guid speciesId) =>
        new($"""id="{treePrefix}-{speciesId}"[^>]*checked""", RegexOptions.None, TimeSpan.FromSeconds(5));

    private static async Task<HttpClient> SignedInClientWithoutRedirectsAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await client.GetAsync(CdcWebTestFactory.TestSignInPath);

        return client;
    }

    // Loads the form first, as a browser would, to pick up the antiforgery token the post needs.
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, Dictionary<string, string[]> fields)
    {
        var page = await client.GetStringAsync(url);
        var token = AntiforgeryTokenRegex().Match(page);
        Assert.True(token.Success, "No antiforgery token found on the Profile properties page.");

        var form = new List<KeyValuePair<string, string>> { new("__RequestVerificationToken", token.Groups[1].Value) };
        form.AddRange(fields.SelectMany(field => field.Value.Select(value => new KeyValuePair<string, string>(field.Key, value))));

        return await client.PostAsync(url, new FormUrlEncodedContent(form));
    }

    private static WebApplicationFactory<Program> CreateFactory(FakeProfilePropertiesApiService properties)
    {
        var manageProfile = new ManageProfileViewModel
        {
            ProfileId = ProfileId,
            ProfileTitle = "Bovine tuberculosis",
            CurrentProfileVersionId = Guid.NewGuid(),
            LatestVersionId = CloneVersionId,
            LinkVisibility = new ManageProfileLinkVisibilityDto { CanEditProperties = true, CanCloneNewProfile = true, CanCloneNewScenario = true }
        };

        return new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(
                    profileAttributes: Profile(),
                    manageProfile: manageProfile));
                services.RemoveAll<IProfilePropertiesApiService>();
                services.AddSingleton<IProfilePropertiesApiService>(properties);
                services.RemoveAll<ISpeciesApiService>();
                services.AddSingleton<ISpeciesApiService>(new FakeSpeciesApiService(Species));
            }));
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*?value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();
}
