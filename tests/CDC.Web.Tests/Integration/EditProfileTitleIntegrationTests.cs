using System.Net;
using System.Text.RegularExpressions;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// Drives the real pipeline - routing, authorization, model binding, antiforgery and the Razor view - for the
// Edit profile title page, with only the CDC.Api client replaced.
public partial class EditProfileTitleIntegrationTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly byte[] CurrentRowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
    private static readonly byte[] StaleRowVersion = [8, 7, 6, 5, 4, 3, 2, 1];

    private static readonly string PageUrl = $"/SurveillanceProfiles/EditProfileTitle/{ProfileId}";

    private static ProfileAttributesDto Profile(string title = "Bovine <em>tuberculosis</em>") => new()
    {
        Id = ProfileId,
        Title = title,
        LastUpdated = CurrentRowVersion
    };

    [Fact]
    public async Task Get_ShowsTheEditorWithTheTitle_AndNoErrors()
    {
        using var factory = CreateFactory(Profile());
        var client = await factory.SignedInClientAsync();

        var page = await client.GetStringAsync(PageUrl);

        Assert.Contains("<title>Edit profile title - D2R2</title>", page);
        Assert.Contains(">Bovine <em>tuberculosis</em></div>", page);
        Assert.Contains("data-app-title-save>Save</button>", page);
        Assert.Contains("<div aria-live=\"polite\">", page);
        Assert.Contains(
            "<p id=\"ProfileTitle-required-error\" class=\"govuk-error-message\" data-app-title-required-error hidden><span class=\"govuk-visually-hidden\">Error:</span> You must enter a profile title</p>",
            page);
        Assert.DoesNotContain("govuk-error-summary", page);
        Assert.DoesNotContain("govuk-input--error", page);
    }

    // Legacy EditProfileTitle.aspx threw unless Profile.CanEditProfile(): a profile editor who is not a user management system account.
    [Theory]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Get_RefusesAUserWhoMayNotEditAProfile(bool isProfileEditor, bool isPolicyProfileUser)
    {
        using var factory = CreateFactory(Profile());
        var client = await factory.SignedInClientWithPermissionsAsync(isProfileEditor, isPolicyProfileUser);

        var response = await client.GetAsync(PageUrl);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("You do not have permission to view this page", body);
        Assert.DoesNotContain("ProfileTitleEditor\"", body);
    }

    [Fact]
    public async Task Post_FromAUserWhoMayNotEditAProfile_IsRefused_AndChangesNothing()
    {
        var api = new Features.Landing.FakeApiClient(profileAttributes: Profile());
        using var factory = CreateFactory(api);
        var client = await factory.SignedInClientWithPermissionsAsync(isProfileEditor: false, isPolicyProfileUser: true);

        var response = await client.PostAsync(PageUrl, new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("ProfileTitle", "Anthrax"),
            new KeyValuePair<string, string>("LastUpdated", Convert.ToBase64String(CurrentRowVersion))
        ]));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(api.LastUpdatedProfileTitle);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<br>")]
    [InlineData("&nbsp;&nbsp;")]
    public async Task Post_WithATitleThatHasNoText_ShowsTheErrorSummaryLinkedToTheEditor_AndTheMessageBesideIt(string title)
    {
        using var factory = CreateFactory(Profile());
        var client = await factory.SignedInClientAsync();

        var response = await PostAsync(client, title, Convert.ToBase64String(CurrentRowVersion));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<title>Error: Edit profile title - D2R2</title>", body);
        Assert.Contains("There is a problem", body);
        Assert.Contains("<a href=\"#ProfileTitleEditor\">You must enter a profile title</a>", body);
        Assert.Contains("<p id=\"ProfileTitle-error\" class=\"govuk-error-message\">", body);
        Assert.Matches("<p id=\"ProfileTitle-error\"[^>]*>\\s*<span class=\"govuk-visually-hidden\">Error:</span> You must enter a profile title\\s*</p>", body);
        Assert.Contains("govuk-form-group--error", body);
        Assert.Contains("govuk-input--error", body);
        Assert.Contains("aria-describedby=\"ProfileTitle-error\"", body);
        Assert.Equal(string.Empty, HiddenValue(body, "ProfileTitle"));
        Assert.DoesNotContain("The profile title was successfully updated", body);
    }

    [Fact]
    public async Task Post_WithATitleOver255Characters_ShowsTheLengthError_AndKeepsWhatWasTyped()
    {
        using var factory = CreateFactory(Profile());
        var client = await factory.SignedInClientAsync();
        var title = new string('a', 256);

        var response = await PostAsync(client, title, Convert.ToBase64String(CurrentRowVersion));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("<a href=\"#ProfileTitleEditor\">Profile title cannot exceed 255 characters</a>", body);
        Assert.Contains($">{title}</div>", body);
        Assert.Equal(title, HiddenValue(body, "ProfileTitle"));
    }

    [Fact]
    public async Task Post_WhenTheApiRefusesTheTitle_ShowsItBesideTheTitle()
    {
        using var factory = CreateFactory(
            Profile(),
            new UpdateProfileTitleResult(UpdateProfileTitleOutcome.ValidationFailed, "The profile title is already in use for a different profile"));
        var client = await factory.SignedInClientAsync();

        var response = await PostAsync(client, "Anthrax", Convert.ToBase64String(CurrentRowVersion));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("<a href=\"#ProfileTitleEditor\">The profile title is already in use for a different profile</a>", body);
        Assert.Contains("<span class=\"govuk-visually-hidden\">Error:</span> The profile title is already in use for a different profile", body);
        Assert.DoesNotContain("Profile save failed", body);
    }

    // The hidden row version comes from the model, not the posted value, so saving again straight away is not refused as stale.
    [Fact]
    public async Task Post_ThatSucceeds_ShowsTheSuccessMessage_AndCarriesTheNewRowVersion()
    {
        using var factory = CreateFactory(Profile("Anthrax"));
        var client = await factory.SignedInClientAsync();

        var response = await PostAsync(client, "Anthrax", Convert.ToBase64String(StaleRowVersion));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("The profile title was successfully updated", body);
        Assert.Equal(Convert.ToBase64String(CurrentRowVersion), HiddenValue(body, "LastUpdated"));
    }

    [Fact]
    public async Task Get_ForAWhatIfScenario_ExplainsItsTitleCannotBeEdited_AndShowsNoForm()
    {
        using var factory = CreateFactory(Profile() with { ParentId = Guid.NewGuid() });
        var client = await factory.SignedInClientAsync();

        var page = await client.GetStringAsync(PageUrl);

        Assert.Contains("<title>Error: Edit profile title - D2R2</title>", page);
        Assert.Contains("You cannot edit the profile title of a &quot;what-if&quot; scenario.", page);
        Assert.DoesNotContain("ProfileTitleEditor\"", page);
        Assert.DoesNotContain("Save</button>", page);
    }

    private static string HiddenValue(string body, string name)
    {
        var match = HiddenInputRegex(name).Match(body);
        Assert.True(match.Success, $"No hidden input named {name} was found.");

        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static Regex HiddenInputRegex(string name) =>
        new($"""<input type="hidden"[^>]*\bname="{name}"[^>]*\bvalue="([^"]*)"[^>]*>""", RegexOptions.None, TimeSpan.FromSeconds(5));

    // Loads the form first, as a browser would, to pick up the antiforgery token the post needs.
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string title, string lastUpdated)
    {
        var page = await client.GetStringAsync(PageUrl);
        var token = AntiforgeryTokenRegex().Match(page);
        Assert.True(token.Success, "No antiforgery token found on the Edit profile title page.");

        return await client.PostAsync(PageUrl, new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("__RequestVerificationToken", token.Groups[1].Value),
            new KeyValuePair<string, string>("ProfileTitle", title),
            new KeyValuePair<string, string>("LastUpdated", lastUpdated)
        ]));
    }

    private static WebApplicationFactory<Program> CreateFactory(ProfileAttributesDto attributes, UpdateProfileTitleResult? updateResult = null) =>
        CreateFactory(new Features.Landing.FakeApiClient(profileAttributes: attributes, updateProfileTitleResult: updateResult));

    private static WebApplicationFactory<Program> CreateFactory(Features.Landing.FakeApiClient apiClient) =>
        new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(apiClient);
            }));

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*?value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();
}
