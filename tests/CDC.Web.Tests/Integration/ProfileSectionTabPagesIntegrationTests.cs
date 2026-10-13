using System.Net;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// Renders each tab of a profile section through the real Razor pipeline (nested layout, tab strip, partials)
// with fakes standing in for CDC.Api, so a view that does not compile or render is caught here.
public class ProfileSectionTabPagesIntegrationTests : IClassFixture<CdcWebTestFactory>
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid SummarySectionId = Guid.NewGuid();
    private static readonly Guid EpidemiologySectionId = Guid.NewGuid();
    private static readonly Guid QuestionId = Guid.NewGuid();
    private static readonly Guid ReviewCommentId = Guid.NewGuid();
    private static readonly Guid YearlyFrequencyId = Guid.NewGuid();
    private static readonly Guid CompleteStatusId = Guid.NewGuid();

    private readonly CdcWebTestFactory _factory;

    public ProfileSectionTabPagesIntegrationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    private static ManageProfileViewModel Profile(bool canViewTabs = true, bool canEdit = true) => new()
    {
        ProfileId = ProfileId,
        ProfileTitle = "Bovine tuberculosis",
        CurrentProfileVersionId = Guid.NewGuid(),
        CanEditProfileSection = canEdit,
        CanViewReviewComments = canViewTabs,
        CanViewContributions = canViewTabs,
        CanViewAllRevisionDates = canViewTabs
    };

    private static ProfileQuestionnaireMetadataDto Metadata() => new()
    {
        Sections =
        [
            new ProfileSectionMetadataDto
            {
                Id = SummarySectionId,
                Name = "Summary",
                ShortName = "SUMMARY",
                SectionNumber = 1,
                Questions =
                [
                    new ProfileQuestionMetadataDto
                    {
                        Id = QuestionId,
                        SectionId = SummarySectionId,
                        ShortName = "Overview",
                        QuestionNumber = 1,
                        Fields = [new ProfileFieldMetadataDto { Id = Guid.NewGuid(), QuestionId = QuestionId, Name = "Is it endemic?", FieldNumber = 1, DataTypeName = "Boolean" }]
                    }
                ]
            },
            new ProfileSectionMetadataDto { Id = EpidemiologySectionId, Name = "Epidemiology", ShortName = "Epi", SectionNumber = 2, Questions = [] }
        ]
    };

    private async Task<string> GetAsync(string page, ManageProfileViewModel profile, string query = "")
    {
        var response = await SendAsync(page, profile, query);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadAsStringAsync();
    }

    private async Task<HttpResponseMessage> SendAsync(string page, ManageProfileViewModel profile, string query = "")
    {
        var noteTypeId = Guid.NewGuid();
        var furtherInformationTypeId = Guid.NewGuid();

        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(new FakeProfileSectionsApiService(
                    metadata: Metadata(),
                    noteTypes:
                    [
                        new ProfileNoteTypeDto { Id = noteTypeId, Name = "Scientific paper reference", PluralName = "Scientific paper references" },
                        new ProfileNoteTypeDto { Id = furtherInformationTypeId, Name = "Source of further information", PluralName = "Sources of further information" }
                    ],
                    notesByNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>>
                    {
                        [noteTypeId] = [new ProfileNoteDto { Id = Guid.NewGuid(), NoteText = "A relevant paper." }],
                        [furtherInformationTypeId] = [new ProfileNoteDto { Id = Guid.NewGuid(), NoteText = "OIE Terrestrial Manual." }]
                    },
                    reviewComments: [new ProfileReviewCommentDto { Id = ReviewCommentId, CommentSubject = "Check the <dates>", CommentReference = "Ref 7", CommentText = "<p>Please <a href=\"https://example.com\">review</a></p><script>alert(1)</script>", UserFullName = "Jane Doe", Level = 1, CanEdit = true, CanDelete = true, LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8], CommentDate = new DateTime(2026, 9, 11, 13, 14, 15) }],
                    contributions: [new ProfileSectionContributionDto { FullName = "John Smith", UserName = "jsmith", OrganisationName = "APHA" }],
                    allRevisionDates: new ProfileVersionAllRevisionDatesDto
                    {
                        Sections =
                        [
                            new ProfileSectionRevisionDatesDto
                            {
                                ProfileSectionId = SummarySectionId,
                                SectionName = "Summary",
                                SectionNumber = 1,
                                VaReviewFrequencyId = YearlyFrequencyId,
                                NextAuthorReview = new DateTime(2027, 1, 1),
                                AuthorReviewStatusId = CompleteStatusId,
                                LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
                            }
                        ],
                        ReviewFrequencies = [new ReviewLookupItemDto { Id = YearlyFrequencyId, Name = "Every year" }],
                        ReviewStatuses = [new ReviewLookupItemDto { Id = CompleteStatusId, Name = "Complete" }]
                    }));
            }));
        var client = factory.CreateClient();
        var signIn = await client.GetAsync(CdcWebTestFactory.TestSignInPath);
        Assert.Equal(HttpStatusCode.NoContent, signIn.StatusCode);

        return await client.GetAsync($"/SurveillanceProfiles/{page}/{ProfileId}{query}");
    }

    [Fact]
    public async Task EditProfileQuestions_ShowsEveryTab_AsAPostingButton_SoTheAnswersAreSavedBeforeNavigating()
    {
        var body = await GetAsync("EditProfileQuestions", Profile());

        Assert.Contains("Is it endemic?", body);
        Assert.Contains(">Summary</button>", body);
        Assert.Contains("name=\"tab\" value=\"References\"", body);
        Assert.Contains("name=\"tab\" value=\"FurtherInformation\"", body);
        Assert.Contains("name=\"tab\" value=\"ReviewComments\"", body);
        Assert.Contains("name=\"tab\" value=\"AllRevisionDates\"", body);
        Assert.Contains("name=\"tab\" value=\"Contributions\"", body);
        Assert.Contains("handler=NavigateTab", body);
        Assert.Contains("govuk-tabs__list-item--selected", body);
    }

    [Fact]
    public async Task EditProfileQuestions_HidesTheTabsTheUserCannotSee()
    {
        var body = await GetAsync("EditProfileQuestions", Profile(canViewTabs: false));

        Assert.Contains("value=\"References\"", body);
        Assert.DoesNotContain("value=\"ReviewComments\"", body);
        Assert.DoesNotContain("value=\"AllRevisionDates\"", body);
        Assert.DoesNotContain("value=\"Contributions\"", body);
    }

    [Fact]
    public async Task EditProfileReferences_RendersTheReferences_WithLinkTabsAndPaginationOnTheSameTab()
    {
        var body = await GetAsync("EditProfileReferences", Profile());

        Assert.Contains("A relevant paper.", body);
        Assert.DoesNotContain("OIE Terrestrial Manual.", body);
        Assert.Contains($"href=\"/SurveillanceProfiles/EditProfileFurtherInformation/{ProfileId}?section={SummarySectionId}\"", body);
        Assert.Contains($"href=\"/SurveillanceProfiles/EditProfileAllRevisionDates/{ProfileId}?section={SummarySectionId}\"", body);
        Assert.Contains("aria-current=\"page\"", body);
        Assert.Contains($"href=\"/SurveillanceProfiles/EditProfileReferences/{ProfileId}?section={EpidemiologySectionId}\"", body);
        Assert.Contains($"href=\"/SurveillanceProfiles/EditProfileQuestions/{ProfileId}?section={EpidemiologySectionId}\"", body);
    }

    [Fact]
    public async Task EditProfileFurtherInformation_RendersTheSourcesOfFurtherInformation()
    {
        var body = await GetAsync("EditProfileFurtherInformation", Profile());

        Assert.Contains("OIE Terrestrial Manual.", body);
        Assert.DoesNotContain("A relevant paper.", body);
    }

    [Fact]
    public async Task EditProfileReviewComments_RendersTheComments()
    {
        var body = await GetAsync("EditProfileReviewComments", Profile());

        Assert.Contains("Check the &lt;dates&gt;", body);
        Assert.DoesNotContain("Check the <dates>", body);
        Assert.Contains("Jane Doe", body);
        Assert.Contains("Ref 7", body);
        Assert.Contains("11/09/26 13:14:15", body);
        Assert.Contains(">Date and time</th>", body);
        Assert.Contains(">Reviewer</th>", body);
        Assert.Contains($"commentId={ReviewCommentId}", body);
        Assert.Contains("Add a review comment:", body);
        Assert.Contains("Items per page:", body);
    }

    [Fact]
    public async Task EditProfileReviewComments_HidesTheAddButton_WhenTheUserCannotEdit()
    {
        var body = await GetAsync("EditProfileReviewComments", Profile(canEdit: false));

        Assert.DoesNotContain("Add a review comment:", body);
        Assert.Contains("Check the", body);
    }

    [Fact]
    public async Task EditProfileReviewComments_ShowsTheAddPanel_WithTheRichTextEditor()
    {
        var body = await GetAsync("EditProfileReviewComments", Profile(), "?mode=add");

        Assert.Contains("Add review comment", body);
        Assert.Contains("name=\"Input.Subject\"", body);
        Assert.Contains("name=\"Input.Reference\"", body);
        Assert.Contains("name=\"Input.CommentText\"", body);
        Assert.Contains("data-richtext-command=\"link\"", body);
        Assert.Contains("data-spellcheck-target=\"Input_CommentText\"", body);
        Assert.Contains("handler=Save", body);
        Assert.Contains("__RequestVerificationToken", body);
        Assert.Contains(">OK</button>", body);
        Assert.Contains(">Cancel</a>", body);
    }

    [Fact]
    public async Task EditProfileReviewComments_ShowsTheViewPanel_WithSanitisedCommentAndTheActionButtons()
    {
        var body = await GetAsync("EditProfileReviewComments", Profile(), $"?commentId={ReviewCommentId}");

        Assert.Contains("View review comment", body);
        Assert.Contains("<a href=\"https://example.com\">review</a>", body);
        Assert.DoesNotContain("<script>alert(1)</script>", body);
        Assert.Contains(">Reply</a>", body);
        Assert.Contains(">Edit</a>", body);
        Assert.Contains("handler=Delete", body);
        Assert.Contains(">Close</a>", body);
        Assert.Contains("Are you sure you want to delete this review comment?", body);
    }

    [Fact]
    public async Task EditProfileReviewComments_ShowsTheEditPanel_PrefilledWithTheComment()
    {
        var body = await GetAsync("EditProfileReviewComments", Profile(), $"?mode=edit&commentId={ReviewCommentId}");

        Assert.Contains("Edit review comment", body);
        Assert.Contains("value=\"Ref 7\"", body);
        Assert.Contains("name=\"Input.LastUpdated\" value=\"AQIDBAUGBwg=\"", body);
    }

    [Fact]
    public async Task EditProfileContributions_RendersTheContributions()
    {
        var body = await GetAsync("EditProfileContributions", Profile());

        Assert.Contains("John Smith", body);
        Assert.Contains("jsmith", body);
    }

    [Theory]
    [InlineData("EditProfileReviewComments")]
    [InlineData("EditProfileContributions")]
    [InlineData("EditProfileAllRevisionDates")]
    public async Task RestrictedTabPages_ReturnNotFound_WhenTheUserCannotViewThem(string page)
    {
        var response = await SendAsync(page, Profile(canViewTabs: false));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task EditProfileAllRevisionDates_RendersTheLegacyLayout_WithSaveAtTheTopAndBottom_WhenEditable()
    {
        var body = await GetAsync("EditProfileAllRevisionDates", Profile());

        Assert.Contains("Set For All Sections:", body);
        Assert.Contains("Author review status:", body);
        Assert.Contains("Next VA review:", body);
        Assert.Contains("Next policy review:", body);
        Assert.Contains("1. Summary", body);
        Assert.Contains("Author review frequency:", body);
        Assert.Contains("Please select...", body);
        Assert.Contains($"name=\"rev_{SummarySectionId:N}_LastUpdated\" value=\"AQIDBAUGBwg=\"", body);
        Assert.Contains("value=\"1 January 2027\"", body);
        Assert.Equal(2, CountOf(body, "handler=SaveRevisionDates"));
        Assert.Contains("revision-dates.js", body);
    }

    [Fact]
    public async Task EditProfileAllRevisionDates_ShowsTheValuesAsText_WithNoSaveOrSetForAll_WhenReadOnly()
    {
        var body = await GetAsync("EditProfileAllRevisionDates", Profile(canEdit: false));

        Assert.DoesNotContain("Set For All Sections:", body);
        Assert.DoesNotContain("handler=SaveRevisionDates", body);
        Assert.Contains("app-revision-dates-readonly", body);
        Assert.Contains("Every year", body);
        Assert.Contains("Complete", body);
        Assert.Contains("1 January 2027", body);
        Assert.DoesNotContain("<select", body);
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;
        var index = text.IndexOf(value, StringComparison.Ordinal);

        while (index >= 0)
        {
            count++;
            index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal);
        }

        return count;
    }
}
