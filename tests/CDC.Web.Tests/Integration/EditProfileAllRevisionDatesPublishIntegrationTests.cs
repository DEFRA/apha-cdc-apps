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

// Drives the real pipeline for the All Revision dates tab: its headings, where Save and the Publish links sit, and the confirmation dialogs
// that post to the Publish handlers and on to the Profile reports page the legacy page redirected to.
public class EditProfileAllRevisionDatesPublishIntegrationTests
{
    private const string PublishPublicQuestion = "Are you sure you want to publish this profile and make it public (this cannot be undone)?";
    private const string PublishDefranetOnlyQuestion = "Are you sure you want to publish this profile but not make it public?";

    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid NewVersionId = Guid.NewGuid();
    private static readonly Guid EpidemiologySectionId = Guid.NewGuid();
    private static readonly Guid WiderSocietySectionId = Guid.NewGuid();
    private static readonly Guid YearlyFrequencyId = Guid.NewGuid();
    private static readonly Guid CompleteStatusId = Guid.NewGuid();

    private static ManageProfileViewModel Profile(bool canPublishPublic = true, bool canPublishDefranetOnly = true, bool canEdit = true) => new()
    {
        ProfileId = ProfileId,
        ProfileTitle = "Anthrax",
        CurrentProfileVersionId = Guid.NewGuid(),
        CanEditProfileSection = canEdit,
        CanEditAllSections = canEdit,
        CanEditAuthorReviewDates = canEdit,
        CanEditVaReviewDates = canEdit,
        CanEditPolicyReviewDates = canEdit,
        CanViewAllRevisionDates = true,
        LinkVisibility = new ManageProfileLinkVisibilityDto { CanPublishPublic = canPublishPublic, CanPublishDefranetOnly = canPublishDefranetOnly }
    };

    private static ProfileQuestionnaireMetadataDto Metadata() => new()
    {
        Sections =
        [
            new ProfileSectionMetadataDto { Id = EpidemiologySectionId, Name = "Epidemiology of animal infection", ShortName = "Epidemiology", SectionNumber = 3, Questions = [] },
            new ProfileSectionMetadataDto { Id = WiderSocietySectionId, Name = "Impact on wider society and stakeholder groups", ShortName = "Wider Society", SectionNumber = 7, Questions = [] }
        ]
    };

    private static ProfileVersionAllRevisionDatesDto RevisionDates() => new()
    {
        Sections =
        [
            new ProfileSectionRevisionDatesDto
            {
                ProfileSectionId = EpidemiologySectionId,
                SectionName = "Epidemiology of animal infection",
                SectionNumber = 3,
                LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
            },
            new ProfileSectionRevisionDatesDto
            {
                ProfileSectionId = WiderSocietySectionId,
                SectionName = "Impact on wider society and stakeholder groups",
                SectionNumber = 7,
                LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
            }
        ],
        ReviewFrequencies = [new ReviewLookupItemDto { Id = YearlyFrequencyId, Name = "Every year" }],
        ReviewStatuses = [new ReviewLookupItemDto { Id = CompleteStatusId, Name = "Complete" }]
    };

    private static WebApplicationFactory<Program> CreateFactory(ManageProfileViewModel profile, PublishProfileResult? publishResult = null)
    {
        var overview = new ProfileReportsOverviewDto { ProfileVersionId = NewVersionId, ProfileId = ProfileId, FullTitle = "Anthrax", VersionLabel = "2.0", IsCurrent = true };

        return new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile, publishProfileResult: publishResult));
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(new FakeProfileSectionsApiService(metadata: Metadata(), allRevisionDates: RevisionDates()));
                services.RemoveAll<IProfileReportsApiService>();
                services.AddSingleton<IProfileReportsApiService>(new FakeProfileReportsApiService(overview: new ProfileReportsOverviewResult(ProfileReportsOutcome.Success, overview)));
            }));
    }

    private static async Task<HttpClient> SignedInClientWithoutRedirectsAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await client.GetAsync(CdcWebTestFactory.TestSignInPath);

        return client;
    }

    private static string TabUrl(Guid sectionId) => $"/SurveillanceProfiles/EditProfileAllRevisionDates/{ProfileId}?section={sectionId}";

    private static async Task<string> GetTabAsync(ManageProfileViewModel profile)
    {
        using var factory = CreateFactory(profile);
        var client = await SignedInClientWithoutRedirectsAsync(factory);

        return await client.GetStringAsync(TabUrl(WiderSocietySectionId));
    }

    private static async Task<HttpResponseMessage> PostPublishAsync(HttpClient client, string handler)
    {
        var page = await client.GetStringAsync(TabUrl(WiderSocietySectionId));
        var token = Regex.Match(page, """name="__RequestVerificationToken"[^>]*value="([^"]+)""", RegexOptions.None, TimeSpan.FromSeconds(5));
        Assert.True(token.Success, "No antiforgery token found on the All Revision dates tab.");

        return await client.PostAsync(
            $"{TabUrl(WiderSocietySectionId)}&handler={handler}",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = token.Groups[1].Value,
                ["ProfileId"] = ProfileId.ToString()
            }));
    }

    // The legacy page printed these headings by section number, not the section's own name.
    [Fact]
    public async Task Tab_HeadsEachSectionWithItsLegacyTitle()
    {
        var body = await GetTabAsync(Profile());

        Assert.Contains("<h3 class=\"govuk-heading-s\">3. Epidemiology</h3>", body);
        Assert.Contains("<h3 class=\"govuk-heading-s\">7. WiderSociety</h3>", body);
        Assert.DoesNotContain("Epidemiology of animal infection", body);
        Assert.DoesNotContain("Impact on wider society and stakeholder groups", body);
    }

    // Legacy: the second Save is the last thing in the box (collapsePanel), and the Publish links are right-aligned beneath the box.
    [Fact]
    public async Task Tab_PutsTheSecondSaveInsideTheBox_AndThePublishLinksBelowIt()
    {
        var body = await GetTabAsync(Profile());

        var box = body.IndexOf("id=\"profile-all-revision-dates-panel\"", StringComparison.Ordinal);
        var firstSave = body.IndexOf("handler=SaveRevisionDates", StringComparison.Ordinal);
        var lastSave = body.LastIndexOf("handler=SaveRevisionDates", StringComparison.Ordinal);
        var formEnd = body.IndexOf("</form>", lastSave, StringComparison.Ordinal);
        var publishPublic = body.IndexOf("Publish (public)", StringComparison.Ordinal);
        var publishDefranetOnly = body.IndexOf("Publish (Defranet only)", StringComparison.Ordinal);

        Assert.True(box >= 0);
        Assert.True(firstSave < box, "The first Save should stay above the tabs.");
        Assert.True(lastSave > box, "The second Save should be inside the box.");
        Assert.Matches(@"handler=SaveRevisionDates[^>]*>Save</button>\s*</div>\s*</div>\s*</div>\s*<input name=""__RequestVerificationToken""[^>]*/></form>", body);
        Assert.True(publishPublic > formEnd, "The Publish links should come after the Save button, outside the page's own form.");
        Assert.True(publishDefranetOnly > publishPublic, "Publish (public) should come before Publish (Defranet only).");
        Assert.Contains("<div class=\"app-publish-links govuk-body\">", body);
    }

    [Fact]
    public async Task Tab_ShowsTheConfirmationDialogOfEachPublishLink_WithOkAndCancel_PostingToTheHandlerOnTheSameSection()
    {
        var body = await GetTabAsync(Profile());

        Assert.Contains("data-confirm-target=\"#confirmPublishPublicModal\"", body);
        Assert.Contains("data-confirm-target=\"#confirmPublishDefranetOnlyModal\"", body);
        Assert.Contains(PublishPublicQuestion, body);
        Assert.Contains(PublishDefranetOnlyQuestion, body);

        foreach (var (modalId, handler) in new[] { ("confirmPublishPublicModal", "PublishPublic"), ("confirmPublishDefranetOnlyModal", "PublishDefranetOnly") })
        {
            var dialog = Regex.Match(body, $"<dialog class=\"app-modal\" id=\"{modalId}\"[\\s\\S]*?</dialog>", RegexOptions.None, TimeSpan.FromSeconds(5)).Value;
            var action = WebUtility.HtmlDecode(Regex.Match(dialog, "<form[^>]*action=\"([^\"]*)\"", RegexOptions.None, TimeSpan.FromSeconds(5)).Groups[1].Value);

            Assert.NotEmpty(dialog);
            Assert.Contains($"handler={handler}", action);
            Assert.Contains($"section={WiderSocietySectionId}", action);
            Assert.Contains(">Ok</button>", dialog);
            Assert.Contains("data-confirm-close>Cancel</button>", dialog);
        }
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Tab_ShowsOnlyTheLinksTheUserMayUse(bool canPublishPublic, bool canPublishDefranetOnly)
    {
        var body = await GetTabAsync(Profile(canPublishPublic, canPublishDefranetOnly));

        Assert.Equal(canPublishPublic, body.Contains("Publish (public)", StringComparison.Ordinal));
        Assert.Equal(canPublishDefranetOnly, body.Contains("Publish (Defranet only)", StringComparison.Ordinal));
        Assert.Equal(canPublishPublic, body.Contains("confirmPublishPublicModal", StringComparison.Ordinal));
        Assert.Equal(canPublishDefranetOnly, body.Contains("confirmPublishDefranetOnlyModal", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Tab_ShowsNoPublishLinks_WhenTheProfileCannotBePublished()
    {
        var body = await GetTabAsync(Profile(canPublishPublic: false, canPublishDefranetOnly: false));

        Assert.DoesNotContain("Publish (public)", body);
        Assert.DoesNotContain("Publish (Defranet only)", body);
        Assert.DoesNotContain("app-publish-links", body);
        Assert.DoesNotContain("confirmPublishPublicModal", body);
        Assert.DoesNotContain("confirmPublishDefranetOnlyModal", body);
    }

    [Fact]
    public async Task Tab_ShowsThePublishLinksButNoSave_ForAUserWhoCanOnlyRead()
    {
        var body = await GetTabAsync(Profile(canEdit: false));

        Assert.DoesNotContain("handler=SaveRevisionDates", body);
        Assert.Contains("Publish (public)", body);
        Assert.Contains("Publish (Defranet only)", body);
    }

    [Theory]
    [InlineData("PublishPublic", "Successfully published this profile and made it public.")]
    [InlineData("PublishDefranetOnly", "Successfully published this profile but did not make it public.")]
    public async Task Publish_RedirectsToTheNewVersionsProfileReports_WhichShowsTheSuccessMessageOnce(string handler, string message)
    {
        using var factory = CreateFactory(Profile(), new PublishProfileResult(PublishProfileOutcome.Success, NewVersionId, null));
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
    [InlineData("PublishPublic")]
    [InlineData("PublishDefranetOnly")]
    public async Task Publish_ListsTheValidationErrorsOnTheTab_OnTheSameSection_WhenTheDraftIsInvalid(string handler)
    {
        ProfileSectionInvalidReasonDto[] reasons =
        [
            new() { SectionNumber = 1, QuestionNumber = 0, Description = "\"Revision dates, Next policy review\" is required" },
            new() { SectionNumber = 3, QuestionNumber = 2, QuestionId = Guid.NewGuid(), Description = "Cattle Row 2, \"Laboratory test - Name\" is required" }
        ];
        using var factory = CreateFactory(Profile(), new PublishProfileResult(PublishProfileOutcome.InvalidProfile, null, null) { InvalidReasons = reasons });
        var client = await SignedInClientWithoutRedirectsAsync(factory);

        var response = await PostPublishAsync(client, handler);
        var body = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-error-summary", body);
        Assert.Contains("There are 2 validation errors", body);
        Assert.Contains("<li>1.0 \"Revision dates, Next policy review\" is required</li>", body);
        Assert.Contains("<li>3.2 Cattle Row 2, \"Laboratory test - Name\" is required</li>", body);
        Assert.Contains("Error: Title: Anthrax", body);
        Assert.DoesNotContain("Successfully published", body);
        Assert.Contains("7. Wider society", Regex.Match(body, "app-subnav__item--current\"[\\s\\S]*?</li>", RegexOptions.None, TimeSpan.FromSeconds(5)).Value);
        Assert.Contains("<h3 class=\"govuk-heading-s\">7. WiderSociety</h3>", body);
    }

    [Fact]
    public async Task Publish_UsesTheSingularHeading_WhenTheDraftHasOneValidationError()
    {
        ProfileSectionInvalidReasonDto[] reasons = [new() { SectionNumber = 2, QuestionNumber = 1, Description = "\"Is it endemic? - Endemic\" is required" }];
        using var factory = CreateFactory(Profile(), new PublishProfileResult(PublishProfileOutcome.InvalidProfile, null, null) { InvalidReasons = reasons });
        var client = await SignedInClientWithoutRedirectsAsync(factory);

        var response = await PostPublishAsync(client, "PublishPublic");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Contains("There is 1 validation error", body);
        Assert.DoesNotContain("validation errors", body);
    }

    [Theory]
    [InlineData("You cannot publish a profile based on an already published version.")]
    [InlineData("You cannot publish this scenario because the profile to which it belongs is not published.")]
    [InlineData("You cannot make this scenario public because there is no public version of this profile.")]
    public async Task Publish_ShowsTheMessageOfTheRuleThatWasBroken_OnTheTab(string message)
    {
        using var factory = CreateFactory(Profile(), new PublishProfileResult(PublishProfileOutcome.Conflict, null, message));
        var client = await SignedInClientWithoutRedirectsAsync(factory);

        var response = await PostPublishAsync(client, "PublishPublic");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(message, body);
        Assert.Contains("There is a problem", body);
        Assert.Contains("Error: Title: Anthrax", body);
        Assert.DoesNotContain("Successfully published", body);
        Assert.DoesNotContain("validation error", body);
        Assert.Contains("<h3 class=\"govuk-heading-s\">3. Epidemiology</h3>", body);
    }

    [Fact]
    public async Task Tab_ShowsNoErrorsAndNoErrorPrefix_WhenItIsOnlyViewed()
    {
        var body = await GetTabAsync(Profile());

        Assert.DoesNotContain("govuk-error-summary", body);
        Assert.DoesNotContain("validation error", body);
        Assert.DoesNotContain("Error: Title", body);
    }

    // The shared confirmation dialog only adds the section to the URL it posts to when it is given one: Manage profile has none.
    [Fact]
    public async Task ManageProfile_ConfirmationDialogs_PostWithoutASectionInTheUrl()
    {
        using var factory = CreateFactory(Profile());
        var client = await SignedInClientWithoutRedirectsAsync(factory);

        var body = await client.GetStringAsync($"/SurveillanceProfiles/ManageProfile/{ProfileId}");
        var action = WebUtility.HtmlDecode(Regex.Match(body, "<form[^>]*action=\"([^\"]*handler=PublishPublic[^\"]*)\"", RegexOptions.None, TimeSpan.FromSeconds(5)).Groups[1].Value);

        Assert.Contains("handler=PublishPublic", action);
        Assert.DoesNotContain("section=", action);
    }
}
