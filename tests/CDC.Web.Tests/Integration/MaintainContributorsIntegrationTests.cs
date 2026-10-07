using System.Text.RegularExpressions;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// The default WebApplicationFactory<Program> has no live CDC.Api, so this page would only ever
// render its "no contributors" empty state. This test swaps in fakes with real, fully populated
// data (including a second page, so the pagination nav itself renders) to exercise the whole view.
public partial class MaintainContributorsIntegrationTests
{
    static MaintainContributorsIntegrationTests() => WebTestEnvironment.EnsureConfigured();

    [Fact]
    public async Task MaintainContributors_RendersTheTableManageProfileLinkAndPagination_WhenContributorsExist()
    {
        var profileId = Guid.NewGuid();
        var profile = new ManageProfileViewModel
        {
            ProfileId = profileId,
            ProfileTitle = "African Horse Sickness (AHS)"
        };
        var contributors = new PagedResult<ContributorDto>
        {
            Items =
            [
                new ContributorDto
                {
                    Id = Guid.NewGuid(),
                    UserName = "carrie.batten",
                    FullName = "Carrie Batten",
                    Organisation = "Pirbright Institute",
                    Role = "Technical author"
                }
            ],
            PageNumber = 2,
            PageSize = 1,
            TotalRecords = 3
        };

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileContributorsApiService>();
                services.AddSingleton<IProfileContributorsApiService>(new FakeProfileContributorsApiService(contributors));
            }));
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/SurveillanceProfiles/MaintainContributors/{profileId}?PageNumber=2&PageSize=1");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("Maintain contributors for", body);
        Assert.Contains("African Horse Sickness (AHS)", body);
        Assert.Contains("Manage profile", body);
        Assert.Contains("carrie.batten", body);
        Assert.Contains("Carrie Batten", body);
        Assert.Contains("Pirbright Institute", body);
        Assert.Contains("Technical author", body);
        Assert.Contains("govuk-pagination", body);
        Assert.Contains(">Add<", body);
    }

    [Fact]
    public async Task MaintainContributors_RendersTheEmptyState_WhenTheProfileHasNoContributors()
    {
        var profileId = Guid.NewGuid();
        var profile = new ManageProfileViewModel { ProfileId = profileId, ProfileTitle = "Avian influenza" };

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileContributorsApiService>();
                services.AddSingleton<IProfileContributorsApiService>(new FakeProfileContributorsApiService());
            }));
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/SurveillanceProfiles/MaintainContributors/{profileId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("This profile has no contributors.", body);
    }

    [Fact]
    public async Task MaintainContributors_RendersTheErrorSummary_WhenTheProfileFailsToLoad()
    {
        var profileId = Guid.NewGuid();

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(throwOnGetManageProfile: new HttpRequestException("boom")));
                services.RemoveAll<IProfileContributorsApiService>();
                services.AddSingleton<IProfileContributorsApiService>(new FakeProfileContributorsApiService());
            }));
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/SurveillanceProfiles/MaintainContributors/{profileId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("There is a problem", body);
        Assert.Contains("Unable to load contributors for this profile", body);
    }

    [Fact]
    public async Task MaintainContributors_RendersTheEditPanel_WhenEditContributorIdIsSet()
    {
        var profileId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();
        var contributorRoleId = Guid.NewGuid();
        var reviewerRoleId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var profile = new ManageProfileViewModel { ProfileId = profileId, ProfileTitle = "African Horse Sickness (AHS)" };
        var contributors = new PagedResult<ContributorDto>
        {
            Items =
            [
                new ContributorDto
                {
                    Id = contributorId,
                    UserName = "carrie.batten",
                    FullName = "Carrie Batten",
                    Organisation = "Pirbright Institute",
                    Role = "Technical author"
                }
            ],
            PageNumber = 1,
            PageSize = 10,
            TotalRecords = 1
        };
        var contributorForEdit = new ContributorEditDto
        {
            Id = contributorId,
            UserName = "carrie.batten",
            FullName = "Carrie Batten",
            Organisation = "Pirbright Institute",
            RoleId = contributorRoleId,
            IsSsoUser = false,
            SectionPermissionIds = [sectionId],
            LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
        };
        IReadOnlyList<ProfileUserRoleDto> roles =
        [
            new() { Id = contributorRoleId, Name = "Technical author", IsContributor = true },
            new() { Id = reviewerRoleId, Name = "Reviewer", IsContributor = false }
        ];
        var metadata = new ProfileQuestionnaireMetadataDto
        {
            Sections = [new ProfileSectionMetadataDto { Id = sectionId, Name = "Epidemiology", ShortName = "Epi", SectionNumber = 1 }]
        };

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileContributorsApiService>();
                services.AddSingleton<IProfileContributorsApiService>(
                    new FakeProfileContributorsApiService(contributors, contributorForEdit: contributorForEdit, profileUserRoles: roles));
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(new FakeProfileSectionsApiService(metadata));
            }));
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/SurveillanceProfiles/MaintainContributors/{profileId}?EditContributorId={contributorId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("Edit profile contributor", body);
        Assert.Contains("Technical author", body);
        Assert.Contains("Reviewer", body);
        Assert.Contains("Epi", body);
        Assert.Contains("Tick all", body);
        Assert.Contains("Untick all", body);
    }

    [Fact]
    public async Task MaintainContributors_RendersReadOnlyFields_WhenTheContributorIsAnSsoUser()
    {
        var profileId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var profile = new ManageProfileViewModel { ProfileId = profileId, ProfileTitle = "African Horse Sickness (AHS)" };
        var contributorForEdit = new ContributorEditDto
        {
            Id = contributorId,
            UserName = "carrie.batten",
            FullName = "Carrie Batten",
            Organisation = "Pirbright Institute",
            RoleId = roleId,
            IsSsoUser = true,
            SectionPermissionIds = [sectionId],
            LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
        };
        IReadOnlyList<ProfileUserRoleDto> roles = [new() { Id = roleId, Name = "Technical author", IsContributor = true }];
        var metadata = new ProfileQuestionnaireMetadataDto
        {
            Sections = [new ProfileSectionMetadataDto { Id = sectionId, Name = "Epidemiology", ShortName = "Epi", SectionNumber = 1 }]
        };

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileContributorsApiService>();
                services.AddSingleton<IProfileContributorsApiService>(
                    new FakeProfileContributorsApiService(contributorForEdit: contributorForEdit, profileUserRoles: roles));
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(new FakeProfileSectionsApiService(metadata));
            }));
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/SurveillanceProfiles/MaintainContributors/{profileId}?EditContributorId={contributorId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("""id="FullName" type="text" value="Carrie Batten" readonly disabled""", body);
        Assert.Contains("""id="Organisation" type="text" value="Pirbright Institute" readonly disabled""", body);
    }

    [Fact]
    public async Task MaintainContributors_RendersTheErrorSummary_WhenSavingFailsValidation()
    {
        var profileId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var profile = new ManageProfileViewModel { ProfileId = profileId, ProfileTitle = "African Horse Sickness (AHS)" };
        var contributorForEdit = new ContributorEditDto
        {
            Id = contributorId,
            UserName = "carrie.batten",
            FullName = "Carrie Batten",
            Organisation = "Pirbright Institute",
            RoleId = roleId,
            IsSsoUser = false,
            SectionPermissionIds = [sectionId],
            LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
        };
        IReadOnlyList<ProfileUserRoleDto> roles = [new() { Id = roleId, Name = "Technical author", IsContributor = true }];
        var metadata = new ProfileQuestionnaireMetadataDto
        {
            Sections = [new ProfileSectionMetadataDto { Id = sectionId, Name = "Epidemiology", ShortName = "Epi", SectionNumber = 1 }]
        };
        var updateResult = new UpdateContributorResult
        {
            Outcome = ContributorUpdateOutcome.ValidationFailed,
            ErrorMessage = "Please select a valid role."
        };

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileContributorsApiService>();
                services.AddSingleton<IProfileContributorsApiService>(
                    new FakeProfileContributorsApiService(contributorForEdit: contributorForEdit, profileUserRoles: roles, updateContributorResult: updateResult));
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(new FakeProfileSectionsApiService(metadata));
            }));
        var client = factory.CreateClient();

        var getResponse = await client.GetAsync(
            $"/SurveillanceProfiles/MaintainContributors/{profileId}?EditContributorId={contributorId}");
        var token = await ExtractAntiforgeryTokenAsync(getResponse);

        var postResponse = await client.PostAsync(
            $"/SurveillanceProfiles/MaintainContributors/{profileId}?handler=SaveContributor",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["ContributorId"] = contributorId.ToString(),
                ["PageNumber"] = "1",
                ["PageSize"] = "10",
                ["LastUpdated"] = Convert.ToBase64String(contributorForEdit.LastUpdated),
                ["RoleId"] = roleId.ToString(),
                ["FullName"] = "Carrie Batten",
                ["Organisation"] = "Pirbright Institute",
                ["SelectedSectionIds"] = sectionId.ToString(),
                ["__RequestVerificationToken"] = token
            }));
        var postBody = await postResponse.Content.ReadAsStringAsync();

        Assert.True(postResponse.IsSuccessStatusCode);
        Assert.Contains("There is a problem", postBody);
        Assert.Contains("Please select a valid role.", postBody);
    }

    [Fact]
    public async Task MaintainContributors_RendersTheSuccessBanner_AfterSavingSucceeds()
    {
        var profileId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var profile = new ManageProfileViewModel { ProfileId = profileId, ProfileTitle = "African Horse Sickness (AHS)" };
        var contributorForEdit = new ContributorEditDto
        {
            Id = contributorId,
            UserName = "carrie.batten",
            FullName = "Carrie Batten",
            Organisation = "Pirbright Institute",
            RoleId = roleId,
            IsSsoUser = false,
            SectionPermissionIds = [sectionId],
            LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
        };
        IReadOnlyList<ProfileUserRoleDto> roles = [new() { Id = roleId, Name = "Technical author", IsContributor = true }];
        var metadata = new ProfileQuestionnaireMetadataDto
        {
            Sections = [new ProfileSectionMetadataDto { Id = sectionId, Name = "Epidemiology", ShortName = "Epi", SectionNumber = 1 }]
        };
        var updateResult = new UpdateContributorResult { Outcome = ContributorUpdateOutcome.Success };

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileContributorsApiService>();
                services.AddSingleton<IProfileContributorsApiService>(
                    new FakeProfileContributorsApiService(contributorForEdit: contributorForEdit, profileUserRoles: roles, updateContributorResult: updateResult));
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(new FakeProfileSectionsApiService(metadata));
            }));
        var client = factory.CreateClient();

        var getResponse = await client.GetAsync(
            $"/SurveillanceProfiles/MaintainContributors/{profileId}?EditContributorId={contributorId}");
        var token = await ExtractAntiforgeryTokenAsync(getResponse);

        var postResponse = await client.PostAsync(
            $"/SurveillanceProfiles/MaintainContributors/{profileId}?handler=SaveContributor",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["ContributorId"] = contributorId.ToString(),
                ["PageNumber"] = "1",
                ["PageSize"] = "10",
                ["LastUpdated"] = Convert.ToBase64String(contributorForEdit.LastUpdated),
                ["RoleId"] = roleId.ToString(),
                ["FullName"] = "Carrie Batten",
                ["Organisation"] = "Pirbright Institute",
                ["SelectedSectionIds"] = sectionId.ToString(),
                ["__RequestVerificationToken"] = token
            }));
        var postBody = await postResponse.Content.ReadAsStringAsync();

        Assert.True(postResponse.IsSuccessStatusCode);
        Assert.Contains("govuk-notification-banner--success", postBody);
        Assert.Contains("Your changes were successfully saved", postBody);
    }

    [Fact]
    public async Task MaintainContributors_RendersTheAddLookupStep_WhenAddContributorIsSet()
    {
        var profileId = Guid.NewGuid();
        var profile = new ManageProfileViewModel { ProfileId = profileId, ProfileTitle = "African Horse Sickness (AHS)" };

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileContributorsApiService>();
                services.AddSingleton<IProfileContributorsApiService>(new FakeProfileContributorsApiService());
            }));
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/SurveillanceProfiles/MaintainContributors/{profileId}?AddContributor=true");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains("Add profile contributor", body);
        Assert.Contains("Look up", body);
    }

    [Fact]
    public async Task MaintainContributors_RendersDeleteConfirmationDialogs_ForEachContributor()
    {
        var profileId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();
        var profile = new ManageProfileViewModel { ProfileId = profileId, ProfileTitle = "African Horse Sickness (AHS)" };
        var contributors = new PagedResult<ContributorDto>
        {
            Items =
            [
                new ContributorDto
                {
                    Id = contributorId,
                    UserName = "carrie.batten",
                    FullName = "Carrie Batten",
                    Organisation = "Pirbright Institute",
                    Role = "Technical author",
                    LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
                }
            ],
            PageNumber = 1,
            PageSize = 10,
            TotalRecords = 1
        };

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileContributorsApiService>();
                services.AddSingleton<IProfileContributorsApiService>(new FakeProfileContributorsApiService(contributors));
            }));
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/SurveillanceProfiles/MaintainContributors/{profileId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode);
        Assert.Contains($"id=\"delete-contributor-{contributorId}\"", body);
        Assert.Contains("Are you sure you want to delete this contributor?", body);
    }

    [Fact]
    public async Task MaintainContributors_RendersStep2_AfterLookupFindsAnExistingUser()
    {
        var profileId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var profile = new ManageProfileViewModel { ProfileId = profileId, ProfileTitle = "African Horse Sickness (AHS)" };
        var contributorForEdit = new ContributorEditDto
        {
            Id = contributorId,
            UserName = "carrie.batten",
            FullName = "Carrie Batten",
            Organisation = "Pirbright Institute",
            RoleId = Guid.Empty,
            IsSsoUser = false,
            SectionPermissionIds = [],
            LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
        };
        IReadOnlyList<ProfileUserRoleDto> roles = [new() { Id = roleId, Name = "Technical author", IsContributor = true }];
        var metadata = new ProfileQuestionnaireMetadataDto
        {
            Sections = [new ProfileSectionMetadataDto { Id = sectionId, Name = "Epidemiology", ShortName = "Epi", SectionNumber = 1 }]
        };
        var verification = new UserVerificationResultDto { Outcome = UserVerificationOutcome.ExistingUser, UserId = contributorId };

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileContributorsApiService>();
                services.AddSingleton<IProfileContributorsApiService>(new FakeProfileContributorsApiService(
                    contributorForEdit: contributorForEdit, profileUserRoles: roles, verificationResult: verification));
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(new FakeProfileSectionsApiService(metadata));
            }));
        var client = factory.CreateClient();

        var getResponse = await client.GetAsync($"/SurveillanceProfiles/MaintainContributors/{profileId}?AddContributor=true");
        var token = await ExtractAntiforgeryTokenAsync(getResponse);

        var postResponse = await client.PostAsync(
            $"/SurveillanceProfiles/MaintainContributors/{profileId}?handler=LookupUser",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["PageNumber"] = "1",
                ["PageSize"] = "10",
                ["AddContributor"] = "true",
                ["LookupUserName"] = "internal\\carrie.batten",
                ["__RequestVerificationToken"] = token
            }));
        var postBody = await postResponse.Content.ReadAsStringAsync();

        Assert.True(postResponse.IsSuccessStatusCode);
        Assert.Contains("Technical author", postBody);
        Assert.Contains("Epi", postBody);
    }

    [Fact]
    public async Task MaintainContributors_RendersAWarning_AfterLookupFindsANewUser()
    {
        var profileId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var profile = new ManageProfileViewModel { ProfileId = profileId, ProfileTitle = "African Horse Sickness (AHS)" };
        IReadOnlyList<ProfileUserRoleDto> roles = [new() { Id = roleId, Name = "Technical author", IsContributor = true }];
        var metadata = new ProfileQuestionnaireMetadataDto
        {
            Sections = [new ProfileSectionMetadataDto { Id = sectionId, Name = "Epidemiology", ShortName = "Epi", SectionNumber = 1 }]
        };
        var verification = new UserVerificationResultDto { Outcome = UserVerificationOutcome.NewUser };

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileContributorsApiService>();
                services.AddSingleton<IProfileContributorsApiService>(new FakeProfileContributorsApiService(
                    profileUserRoles: roles, verificationResult: verification));
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(new FakeProfileSectionsApiService(metadata));
            }));
        var client = factory.CreateClient();

        var getResponse = await client.GetAsync($"/SurveillanceProfiles/MaintainContributors/{profileId}?AddContributor=true");
        var token = await ExtractAntiforgeryTokenAsync(getResponse);

        var postResponse = await client.PostAsync(
            $"/SurveillanceProfiles/MaintainContributors/{profileId}?handler=LookupUser",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["PageNumber"] = "1",
                ["PageSize"] = "10",
                ["AddContributor"] = "true",
                ["LookupUserName"] = "internal\\new.user",
                ["__RequestVerificationToken"] = token
            }));
        var postBody = await postResponse.Content.ReadAsStringAsync();

        Assert.True(postResponse.IsSuccessStatusCode);
        Assert.Contains("The user is not currently in the profiles system.", postBody);
        Assert.Contains("govuk-warning-text", postBody);
    }

    [Fact]
    public async Task MaintainContributors_RendersAnError_AfterLookupIsBlocked()
    {
        var profileId = Guid.NewGuid();
        var profile = new ManageProfileViewModel { ProfileId = profileId, ProfileTitle = "African Horse Sickness (AHS)" };
        var verification = new UserVerificationResultDto { Outcome = UserVerificationOutcome.Blocked };

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileContributorsApiService>();
                services.AddSingleton<IProfileContributorsApiService>(new FakeProfileContributorsApiService(verificationResult: verification));
            }));
        var client = factory.CreateClient();

        var getResponse = await client.GetAsync($"/SurveillanceProfiles/MaintainContributors/{profileId}?AddContributor=true");
        var token = await ExtractAntiforgeryTokenAsync(getResponse);

        var postResponse = await client.PostAsync(
            $"/SurveillanceProfiles/MaintainContributors/{profileId}?handler=LookupUser",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["PageNumber"] = "1",
                ["PageSize"] = "10",
                ["AddContributor"] = "true",
                ["LookupUserName"] = "internal\\admin",
                ["__RequestVerificationToken"] = token
            }));
        var postBody = await postResponse.Content.ReadAsStringAsync();

        Assert.True(postResponse.IsSuccessStatusCode);
        Assert.Contains("This user cannot be made a contributor.", postBody);
    }

    [Fact]
    public async Task MaintainContributors_RendersTheSuccessBanner_AfterAddingSucceeds()
    {
        var profileId = Guid.NewGuid();
        var profile = new ManageProfileViewModel { ProfileId = profileId, ProfileTitle = "African Horse Sickness (AHS)" };
        var addResult = new AddContributorResult { Outcome = ContributorAddOutcome.Success };

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileContributorsApiService>();
                services.AddSingleton<IProfileContributorsApiService>(new FakeProfileContributorsApiService(addContributorResult: addResult));
            }));
        var client = factory.CreateClient();

        var getResponse = await client.GetAsync($"/SurveillanceProfiles/MaintainContributors/{profileId}?AddContributor=true");
        var token = await ExtractAntiforgeryTokenAsync(getResponse);

        var postResponse = await client.PostAsync(
            $"/SurveillanceProfiles/MaintainContributors/{profileId}?handler=AddContributor",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["ContributorId"] = Guid.NewGuid().ToString(),
                ["LookupUserName"] = "internal\\new.user",
                ["IsSsoUser"] = "false",
                ["IsNewUser"] = "true",
                ["PageNumber"] = "1",
                ["PageSize"] = "10",
                ["RoleId"] = Guid.NewGuid().ToString(),
                ["FullName"] = "New User",
                ["Organisation"] = "Pirbright Institute",
                ["__RequestVerificationToken"] = token
            }));
        var postBody = await postResponse.Content.ReadAsStringAsync();

        Assert.True(postResponse.IsSuccessStatusCode);
        Assert.Contains("govuk-notification-banner--success", postBody);
        Assert.Contains("The contributor was successfully added", postBody);
    }

    [Fact]
    public async Task MaintainContributors_RendersTheSuccessBanner_AfterDeletingSucceeds()
    {
        var profileId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();
        var profile = new ManageProfileViewModel { ProfileId = profileId, ProfileTitle = "African Horse Sickness (AHS)" };
        var contributors = new PagedResult<ContributorDto>
        {
            Items =
            [
                new ContributorDto
                {
                    Id = contributorId,
                    UserName = "carrie.batten",
                    FullName = "Carrie Batten",
                    Organisation = "Pirbright Institute",
                    Role = "Technical author",
                    LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
                }
            ],
            PageNumber = 1,
            PageSize = 10,
            TotalRecords = 1
        };
        var deleteResult = new DeleteContributorResult { Outcome = ContributorDeleteOutcome.Success };

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileContributorsApiService>();
                services.AddSingleton<IProfileContributorsApiService>(
                    new FakeProfileContributorsApiService(contributors, deleteContributorResult: deleteResult));
            }));
        var client = factory.CreateClient();

        var getResponse = await client.GetAsync($"/SurveillanceProfiles/MaintainContributors/{profileId}");
        var token = await ExtractAntiforgeryTokenAsync(getResponse);

        var postResponse = await client.PostAsync(
            $"/SurveillanceProfiles/MaintainContributors/{profileId}?handler=DeleteContributor",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["ContributorId"] = contributorId.ToString(),
                ["LastUpdated"] = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
                ["PageNumber"] = "1",
                ["PageSize"] = "10",
                ["__RequestVerificationToken"] = token
            }));
        var postBody = await postResponse.Content.ReadAsStringAsync();

        Assert.True(postResponse.IsSuccessStatusCode);
        Assert.Contains("govuk-notification-banner--success", postBody);
        Assert.Contains("The user was successfully removed from the list of contributors", postBody);
    }

    [Fact]
    public async Task MaintainContributors_RendersTheErrorSummary_AfterDeletingConflicts()
    {
        var profileId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();
        var profile = new ManageProfileViewModel { ProfileId = profileId, ProfileTitle = "African Horse Sickness (AHS)" };
        var contributors = new PagedResult<ContributorDto>
        {
            Items =
            [
                new ContributorDto
                {
                    Id = contributorId,
                    UserName = "carrie.batten",
                    FullName = "Carrie Batten",
                    Organisation = "Pirbright Institute",
                    Role = "Technical author",
                    LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8]
                }
            ],
            PageNumber = 1,
            PageSize = 10,
            TotalRecords = 1
        };
        var deleteResult = new DeleteContributorResult
        {
            Outcome = ContributorDeleteOutcome.Conflict,
            ErrorMessage = "Another user has changed this contributor since the page was loaded. Reload and try again."
        };

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileContributorsApiService>();
                services.AddSingleton<IProfileContributorsApiService>(
                    new FakeProfileContributorsApiService(contributors, deleteContributorResult: deleteResult));
            }));
        var client = factory.CreateClient();

        var getResponse = await client.GetAsync($"/SurveillanceProfiles/MaintainContributors/{profileId}");
        var token = await ExtractAntiforgeryTokenAsync(getResponse);

        var postResponse = await client.PostAsync(
            $"/SurveillanceProfiles/MaintainContributors/{profileId}?handler=DeleteContributor",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["ContributorId"] = contributorId.ToString(),
                ["LastUpdated"] = Convert.ToBase64String([1, 2, 3, 4, 5, 6, 7, 8]),
                ["PageNumber"] = "1",
                ["PageSize"] = "10",
                ["__RequestVerificationToken"] = token
            }));
        var postBody = await postResponse.Content.ReadAsStringAsync();

        Assert.True(postResponse.IsSuccessStatusCode);
        Assert.Contains("Another user has changed this contributor since the page was loaded. Reload and try again.", postBody);
    }

    private static async Task<string> ExtractAntiforgeryTokenAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        var match = AntiforgeryTokenRegex().Match(body);

        Assert.True(match.Success, "Could not find the antiforgery token in the response body.");

        return match.Groups[1].Value;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();
}
