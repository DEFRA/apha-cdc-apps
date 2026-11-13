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

    // As the legacy table did, the title box sits to the right of its label; its formatting toolbar shows only while editing,
    // and the button is offered when the page opens (even with no title) and hidden only once the title loses focus empty,
    // with the legacy required message and asterisk ready for the scripts to show.
    [Fact]
    public async Task CreatePage_PutsTheTitleBoxBesideItsLabel_WithTheEditingAidsTheScriptsUse()
    {
        using var factory = CreateFactory(new FakeProfilePropertiesApiService());
        var client = await factory.SignedInClientAsync();

        var page = await client.GetStringAsync("/SurveillanceProfiles/ProfileProperties");

        Assert.Matches(
            "<div class=\"app-edit-title-row app-edit-title-row--message-below\">\\s*<label class=\"govuk-label app-edit-title-row__label app-edit-title-row__label--quarter\" id=\"ProfileTitleLabel\" for=\"ProfileTitleEditor\">Profile title:<span class=\"app-validator-marker\" data-app-title-required-mark title=\"You must enter a title\" aria-hidden=\"true\" hidden=\"hidden\">\\*</span></label>\\s*<div class=\"app-edit-title-row__input-wrapper\">",
            page);
        Assert.Contains("data-app-title-toolbar=\"ProfileTitleEditor\" data-app-title-toolbar-on-focus role=\"toolbar\"", page);
        Assert.Contains(
            "<p id=\"ProfileTitle-required-error\" class=\"govuk-error-message\" data-app-title-required-error hidden=\"hidden\"><span class=\"govuk-visually-hidden\">Error:</span> You must enter a title</p>",
            page);
        Assert.Contains("data-app-title-save=\"on-blur\">Create profile</button>", page);
        Assert.DoesNotContain("data-app-title-save=\"on-load\"", page);
    }

    // As in legacy, the page opens with no message or asterisk against the title or the profiled species; the scripts flag them
    // when the person leaves them empty, or resets the page.
    [Fact]
    public async Task CreatePage_OpensWithoutAMessageOrAsteriskAgainstTheTitleOrTheProfiledSpecies()
    {
        using var factory = CreateFactory(new FakeProfilePropertiesApiService());
        var client = await factory.SignedInClientAsync();

        var page = await client.GetStringAsync("/SurveillanceProfiles/ProfileProperties");

        Assert.Contains(TitleMark(hidden: true), page);
        Assert.Contains(SpeciesMark(hidden: true), page);
        Assert.DoesNotContain("id=\"ProfileTitle-error\"", page);
        Assert.DoesNotContain("id=\"profiled-species-error\"", page);
        Assert.DoesNotContain("govuk-form-group--error", page);
    }

    // As in legacy, "Selected:" is under both species boxes from the start, even with nothing selected, and only the species named after it are bold.
    [Fact]
    public async Task CreatePage_ShowsSelectedUnderBothSpeciesBoxesFromTheStart_WithOnlyTheSpeciesNamedInBold()
    {
        using var factory = CreateFactory(new FakeProfilePropertiesApiService());
        var client = await factory.SignedInClientAsync();

        var page = await client.GetStringAsync("/SurveillanceProfiles/ProfileProperties");

        foreach (var kind in new[] { "profiled", "other" })
        {
            Assert.Contains(
                $"<output class=\"govuk-body app-species-selector__status\" id=\"{kind}-species-status\" data-species-status=\"{kind}\">Selected: <strong data-species-selected></strong></output>",
                page);
        }
    }

    // As the legacy page showed its validation message under the title box, the required message comes after the textbox, not before it.
    [Fact]
    public async Task CreatePage_PutsTheRequiredMessageBelowTheTitleBox()
    {
        using var factory = CreateFactory(new FakeProfilePropertiesApiService());
        var client = await factory.SignedInClientAsync();

        var page = await client.GetStringAsync("/SurveillanceProfiles/ProfileProperties");

        Assert.Matches("id=\"ProfileTitleEditor\"[\\s\\S]*?id=\"ProfileTitle-required-error\"", page);
    }

    // The Search page links to Clone scenario with the legacy Scenario=1; Manage profile uses true. Legacy's required message does not name the kind of title.
    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    public async Task CloneScenarioPage_NamesTheScenarioTitleInTheLabel_AndUsesTheLegacyRequiredMessage(string scenario)
    {
        var properties = new FakeProfilePropertiesApiService(defaults: new NewProfileDefaultsDto { ScenarioTitle = "Outbreak", ParentTitle = "Anthrax" });
        using var factory = CreateFactory(properties);
        var client = await factory.SignedInClientAsync();

        var page = await client.GetStringAsync($"/SurveillanceProfiles/ProfileProperties?cloneProfileVersionId={CloneVersionId}&scenario={scenario}");

        Assert.Equal((CloneVersionId, true), properties.LastDefaultsRequest);
        Assert.Contains("for=\"ProfileTitleEditor\">Scenario title:<span", page);
        Assert.Contains("Error:</span> You must enter a title</p>", page);
        Assert.DoesNotContain("You must enter a scenario title", page);
        Assert.Contains("Outbreak</div>", page);
    }

    [Fact]
    public async Task EditPage_OffersTheUpdateButtonUntilTheTitleLosesFocusEmpty()
    {
        using var factory = CreateFactory(new FakeProfilePropertiesApiService());
        var client = await factory.SignedInClientAsync();

        var page = await client.GetStringAsync($"/SurveillanceProfiles/ProfileProperties/{ProfileId}");

        Assert.Contains("data-app-title-save=\"on-blur\">Update</button>", page);
    }

    // As the legacy page did, each species box holds its own "Close all" above its tree, rather than one control above both boxes.
    [Fact]
    public async Task EditPage_PutsACloseAllToggleInsideEachSpeciesBox_AboveItsTree()
    {
        using var factory = CreateFactory(new FakeProfilePropertiesApiService());
        var client = await factory.SignedInClientAsync();

        var page = await client.GetStringAsync($"/SurveillanceProfiles/ProfileProperties/{ProfileId}");

        foreach (var box in new[] { "profiled", "other" })
        {
            Assert.Matches(
                $"data-species-tree=\"{box}\">\\s*<div class=\"app-tree__controls\">\\s*<button type=\"button\" class=\"app-tree__toggle-all\" data-app-tree-action=\"toggle-all\" aria-expanded=\"true\">[\\s\\S]*?Close all</span>\\s*</button>\\s*</div>\\s*<div class=\"app-species-selector__tree\">\\s*<ul class=\"app-tree\"",
                page);
        }

        Assert.Equal(2, Regex.Count(page, "data-app-tree-action=\"toggle-all\"", RegexOptions.None, TimeSpan.FromSeconds(5)));
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
    public async Task Create_ReloadsTheNewProfilesProperties_WithTheSpeciesTheUserTicked_AndShowsTheSuccessMessageOnce()
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
        var url = $"/SurveillanceProfiles/ProfileProperties/{request.Id}";
        Assert.Equal(url, response.Headers.Location?.OriginalString);
        Assert.Equal("Anthrax", request.Title);
        Assert.Equal(["Profiled", "Other"], request.AffectedSpeciesInsertList.Select(species => species.Type));

        var reloaded = await client.GetStringAsync(url);
        Assert.Contains("govuk-notification-banner--success", reloaded);
        Assert.Contains("Your changes are successfully saved", reloaded);
        Assert.Contains("data-app-title-save=\"on-blur\">Update</button>", reloaded);
        Assert.DoesNotContain("Use this function to create a brand new Surveillance Profile", reloaded);
        Assert.DoesNotContain("Your changes are successfully saved", await client.GetStringAsync(url));
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
        Assert.Contains("<a href=\"#ProfileTitleEditor\">You must enter a title</a>", body);
        Assert.Contains("<a href=\"#profiled-species-group\">You must specify at least one profiled species</a>", body);
        Assert.Contains("<title>Error: Create new profile", body);
        Assert.Matches(CheckedBoxRegex("other-species", Sheep), body);

        // A missing title or profiled species is shown through the elements the scripts manage, with the legacy asterisks,
        // so the message and the asterisk go as soon as the person puts it right.
        Assert.Matches("id=\"ProfileTitleEditor\"[\\s\\S]*?id=\"ProfileTitle-required-error\"", body);
        Assert.Contains(
            "<p id=\"ProfileTitle-required-error\" class=\"govuk-error-message\" data-app-title-required-error><span class=\"govuk-visually-hidden\">Error:</span> You must enter a title</p>",
            body);
        Assert.DoesNotContain("id=\"ProfileTitle-error\"", body);
        Assert.Contains("aria-describedby=\"ProfileTitle-required-error\"", body);
        Assert.Contains(TitleMark(hidden: false), body);
        Assert.Contains(SpeciesMark(hidden: false), body);
    }

    // A message that is not about a missing title stays a server message, and gets no asterisk.
    [Fact]
    public async Task Create_KeepsAServerMessageForATitleOverTheLimit_WithoutAnAsterisk()
    {
        using var factory = CreateFactory(new FakeProfilePropertiesApiService());
        var client = await SignedInClientWithoutRedirectsAsync(factory);

        var response = await PostAsync(client, "/SurveillanceProfiles/ProfileProperties", new Dictionary<string, string[]>
        {
            ["ProfileTitle"] = [new string('a', 256)],
            ["ProfiledSpeciesIds"] = [Sheep.ToString()]
        });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Matches("<p id=\"ProfileTitle-error\"[^>]*>\\s*<span class=\"govuk-visually-hidden\">Error:</span> Profile title cannot exceed 255 characters\\s*</p>", body);
        Assert.Contains("aria-describedby=\"ProfileTitle-error\"", body);
        Assert.Contains("data-app-title-required-error hidden=\"hidden\"", body);
        Assert.Contains(TitleMark(hidden: true), body);
        Assert.Contains(SpeciesMark(hidden: true), body);
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

    // The legacy asterisks, as rendered: hidden until the page (or a script) flags the title or the profiled species as missing.
    private static string TitleMark(bool hidden) =>
        $"data-app-title-required-mark title=\"You must enter a title\" aria-hidden=\"true\"{(hidden ? " hidden=\"hidden\"" : string.Empty)}>*</span>";

    private static string SpeciesMark(bool hidden) =>
        $"data-species-required-mark title=\"{AffectedSpeciesSelection.NoProfiledSpeciesMessage}\" aria-hidden=\"true\"{(hidden ? " hidden=\"hidden\"" : string.Empty)}>*</span>";

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
