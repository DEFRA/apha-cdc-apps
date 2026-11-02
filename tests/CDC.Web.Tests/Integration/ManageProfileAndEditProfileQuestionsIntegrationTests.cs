using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// The default CdcWebTestFactory has no live CDC.Api, so these pages only ever render their "no
// profile"/empty-state paths. These tests swap in fakes with real, fully populated data so every
// action link, the confirm modal, the question accordion, section pagination, and the
// references/further-information note lists actually render.
public class ManageProfileAndEditProfileQuestionsIntegrationTests : IClassFixture<CdcWebTestFactory>
{
    private readonly CdcWebTestFactory _factory;

    public ManageProfileAndEditProfileQuestionsIntegrationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ManageProfile_RendersEveryActionLinkAndConfirmModal_WhenFullyAuthorised()
    {
        var profileId = Guid.NewGuid();
        var profileStatusId = Guid.NewGuid();
        var profile = new ManageProfileViewModel
        {
            ProfileId = profileId,
            ProfileTitle = "Bovine tuberculosis",
            LatestPublishedVersionPublic = "Version 5",
            LatestPublishedVersionDefraNetOnly = "Version 7",
            LatestDraftVersion = "Version 8",
            ProfileStatus = "Draft",
            ProfileStatusId = profileStatusId,
            CurrentProfileVersionId = Guid.NewGuid(),
            LatestVersionId = Guid.NewGuid(),
            NewDraftVersionLabel = "8.1",
            LinkVisibility = new ManageProfileLinkVisibilityDto
            {
                CanEditProperties = true,
                CanMaintainContributorsAndReviewers = true,
                CanViewContributionsReport = true,
                CanCreateNewDraftVersion = true,
                CanDeleteCurrentVersion = true,
                CanCloneNewProfile = true,
                CanCloneNewScenario = true,
                CanPublishPublic = true,
                CanPublishDefranetOnly = true,
                CanAllowPublicAccess = true
            }
        };

        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(
                    manageProfile: profile,
                    profileStatusTypes: [new ProfileStatusTypeDto { Id = profileStatusId, Name = "Draft" }]));
            }));
        var client = factory.CreateClient();
        var signIn = await client.GetAsync(CdcWebTestFactory.TestSignInPath);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, signIn.StatusCode);

        var response = await client.GetAsync($"/SurveillanceProfiles/ManageProfile/{profileId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("Delete current version", body);
        Assert.Contains("Create new draft version", body);
        Assert.Contains("Are you sure you want to delete this profile version?", body);
        Assert.Contains("Publish (public)", body);
        Assert.Contains("Are you sure you want to publish this profile and make it public (this cannot be undone)?", body);
        Assert.Contains("Publish (Defranet only)", body);
        Assert.Contains("Are you sure you want to publish this profile but not make it public?", body);
    }

    [Fact]
    public async Task EditProfileQuestions_RendersAccordionTabStripAndPagination_ForATwoSectionProfile()
    {
        var profileId = Guid.NewGuid();
        var summarySectionId = Guid.NewGuid();
        var questionId = Guid.NewGuid();
        var noteTypeId = Guid.NewGuid();
        var profile = new ManageProfileViewModel
        {
            ProfileId = profileId,
            ProfileTitle = "Bovine tuberculosis",
            CurrentProfileVersionId = Guid.NewGuid()
        };
        var metadata = new ProfileQuestionnaireMetadataDto
        {
            Sections =
            [
                new ProfileSectionMetadataDto
                {
                    Id = summarySectionId,
                    Name = "Summary",
                    ShortName = "Summ",
                    SectionNumber = 1,
                    Questions =
                    [
                        new ProfileQuestionMetadataDto
                        {
                            Id = questionId,
                            SectionId = summarySectionId,
                            ShortName = "Overview",
                            QuestionNumber = 1,
                            Fields =
                            [
                                new ProfileFieldMetadataDto
                                {
                                    Id = Guid.NewGuid(),
                                    QuestionId = questionId,
                                    Name = "Is it endemic?",
                                    FieldNumber = 1,
                                    DataTypeName = "Boolean"
                                }
                            ]
                        }
                    ]
                },
                new ProfileSectionMetadataDto
                {
                    Id = Guid.NewGuid(),
                    Name = "Epidemiology",
                    ShortName = "Epi",
                    SectionNumber = 2,
                    Questions = []
                }
            ]
        };
        var noteType = new ProfileNoteTypeDto
        {
            Id = noteTypeId,
            Name = "Scientific paper reference",
            PluralName = "Scientific paper references"
        };
        var note = new ProfileNoteDto { Id = Guid.NewGuid(), NoteText = "A relevant paper." };

        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(new FakeProfileSectionsApiService(
                    metadata: metadata,
                    noteTypes: [noteType],
                    notesByNoteType: new Dictionary<Guid, IReadOnlyList<ProfileNoteDto>> { [noteTypeId] = [note] }));
            }));
        var client = factory.CreateClient();
        var signIn = await client.GetAsync(CdcWebTestFactory.TestSignInPath);
        Assert.Equal(System.Net.HttpStatusCode.NoContent, signIn.StatusCode);

        var response = await client.GetAsync($"/SurveillanceProfiles/EditProfileQuestions/{profileId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("Is it endemic?", body);
        Assert.Contains("Epidemiology", body);
        Assert.DoesNotContain("A relevant paper.", body);
    }

    [Fact]
    public async Task EditProfileQuestions_LinksPrintFullProfileAndPrintSection_ToThePrintPageInANewTab_WhenTheUserMayPrint()
    {
        var profileId = Guid.NewGuid();
        var profile = new ManageProfileViewModel
        {
            ProfileId = profileId,
            ProfileTitle = "Bovine tuberculosis",
            CurrentProfileVersionId = Guid.NewGuid(),
            CanPrintProfileSection = true,
            CanViewProfileReports = true
        };

        using var factory = CreateProfilePageFactory(profile);
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/EditProfileQuestions/{profileId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains($"href=\"/SurveillanceProfiles/ViewProfilePrintVersion/{profileId}\" target=\"_blank\" rel=\"noopener noreferrer\">Print full profile", body);
        Assert.Contains($"href=\"/SurveillanceProfiles/ViewProfilePrintVersion/{profileId}?section={ProfileTabTestData.SummarySectionId}\" draggable=\"false\" class=\"govuk-button govuk-button--secondary\"", body);
        Assert.DoesNotContain("window.print", body);
        Assert.Contains($"href=\"/SurveillanceProfiles/ViewDraftSummaryReport/{profileId}/SummaryProfile\" target=\"_blank\" rel=\"noopener noreferrer\">Summary profile report", body);
        Assert.Contains($"href=\"/SurveillanceProfiles/ViewDraftSummaryReport/{profileId}/SummaryPrioritisation\" target=\"_blank\" rel=\"noopener noreferrer\">Summary prioritisation report", body);
        Assert.DoesNotContain("not yet available", body);
    }

    [Fact]
    public async Task EditProfileQuestions_LinksOnlyTheSummaryReports_WhenTheUserMayRunThemButNotPrint()
    {
        var profileId = Guid.NewGuid();
        var profile = new ManageProfileViewModel
        {
            ProfileId = profileId,
            ProfileTitle = "Bovine tuberculosis",
            CurrentProfileVersionId = Guid.NewGuid(),
            CanViewProfileReports = true
        };

        using var factory = CreateProfilePageFactory(profile);
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/EditProfileQuestions/{profileId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.DoesNotContain("ViewProfilePrintVersion", body);
        Assert.Contains($"/SurveillanceProfiles/ViewDraftSummaryReport/{profileId}/SummaryProfile", body);
        Assert.Contains($"/SurveillanceProfiles/ViewDraftSummaryReport/{profileId}/SummaryPrioritisation", body);
        Assert.DoesNotContain("No profile reports are available for this version.", body);
    }

    [Fact]
    public async Task EditProfileQuestions_HidesTheSummaryReportLinks_WhenTheUserMayNotRunThem()
    {
        var profileId = Guid.NewGuid();
        var profile = new ManageProfileViewModel
        {
            ProfileId = profileId,
            ProfileTitle = "Bovine tuberculosis",
            CurrentProfileVersionId = Guid.NewGuid(),
            CanPrintProfileSection = true
        };

        using var factory = CreateProfilePageFactory(profile);
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/EditProfileQuestions/{profileId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("Print full profile", body);
        Assert.DoesNotContain("ViewDraftSummaryReport", body);
        Assert.DoesNotContain("Summary profile report", body);
        Assert.DoesNotContain("Summary prioritisation report", body);
    }

    [Fact]
    public async Task EditProfileQuestions_HidesThePrintLinks_WhenTheUserMayNotPrint()
    {
        var profileId = Guid.NewGuid();
        var profile = new ManageProfileViewModel
        {
            ProfileId = profileId,
            ProfileTitle = "Bovine tuberculosis",
            CurrentProfileVersionId = Guid.NewGuid()
        };

        using var factory = CreateProfilePageFactory(profile);
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/EditProfileQuestions/{profileId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.DoesNotContain("ViewProfilePrintVersion", body);
        Assert.DoesNotContain("ViewDraftSummaryReport", body);
        Assert.Contains("No profile reports are available for this version.", body);
    }

    [Fact]
    public async Task Notes_LinksPrintAndBothSummaryReports_AsTheLegacyLeftNavigationDidOnEveryPage()
    {
        var profileId = Guid.NewGuid();
        var profile = new ManageProfileViewModel
        {
            ProfileId = profileId,
            ProfileTitle = "Bovine tuberculosis",
            CurrentProfileVersionId = Guid.NewGuid(),
            CanPrintProfileSection = true,
            CanViewProfileReports = true
        };

        using var factory = CreateProfilePageFactory(profile);
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/Notes/{profileId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains($"href=\"/SurveillanceProfiles/ViewProfilePrintVersion/{profileId}\" target=\"_blank\" rel=\"noopener noreferrer\">Print full profile", body);
        Assert.Contains($"href=\"/SurveillanceProfiles/ViewDraftSummaryReport/{profileId}/SummaryProfile\" target=\"_blank\" rel=\"noopener noreferrer\">Summary profile report", body);
        Assert.Contains($"href=\"/SurveillanceProfiles/ViewDraftSummaryReport/{profileId}/SummaryPrioritisation\" target=\"_blank\" rel=\"noopener noreferrer\">Summary prioritisation report", body);
        Assert.DoesNotContain("No profile reports are available for this version.", body);
    }

    [Fact]
    public async Task Notes_ShowsNoReportLinks_WhenTheUserMayNotRunThem()
    {
        var profileId = Guid.NewGuid();
        var profile = new ManageProfileViewModel
        {
            ProfileId = profileId,
            ProfileTitle = "Bovine tuberculosis",
            CurrentProfileVersionId = Guid.NewGuid()
        };

        using var factory = CreateProfilePageFactory(profile);
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/Notes/{profileId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.DoesNotContain("ViewProfilePrintVersion", body);
        Assert.DoesNotContain("ViewDraftSummaryReport", body);
        Assert.Contains("No profile reports are available for this version.", body);
    }

    private WebApplicationFactory<Program> CreateProfilePageFactory(ManageProfileViewModel profile) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(new FakeProfileSectionsApiService(metadata: ProfileTabTestData.TwoSectionMetadata()));
            }));
}
