using CDC.Common.Contracts;
using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.Features.Landing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class MaintainContributorsModelTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();

    private static MaintainContributorsModel CreatePageModel(
        ManageProfileViewModel? manageProfile = null,
        Exception? throwOnGetManageProfile = null,
        PagedResult<ContributorDto>? contributors = null,
        Exception? throwOnGetProfileContributors = null,
        ContributorEditDto? contributorForEdit = null,
        IReadOnlyList<ProfileUserRoleDto>? profileUserRoles = null,
        ProfileQuestionnaireMetadataDto? questionnaireMetadata = null,
        UpdateContributorResult? updateContributorResult = null,
        Exception? throwOnUpdateContributor = null,
        Exception? throwOnGetContributorForEdit = null,
        UserVerificationResultDto? verificationResult = null,
        Exception? throwOnVerifyContributorUsername = null,
        AddContributorResult? addContributorResult = null,
        Exception? throwOnAddContributor = null,
        DeleteContributorResult? deleteContributorResult = null,
        Exception? throwOnDeleteContributor = null)
    {
        var modelMetadataProvider = new EmptyModelMetadataProvider();
        var httpContext = new DefaultHttpContext();
        var pageModel = new MaintainContributorsModel(
            new FakeApiClient(manageProfile: manageProfile, throwOnGetManageProfile: throwOnGetManageProfile),
            new FakeProfileContributorsApiService(
                contributors,
                throwOnGetProfileContributors,
                contributorForEdit,
                profileUserRoles,
                updateContributorResult,
                throwOnUpdateContributor,
                throwOnGetContributorForEdit,
                verificationResult,
                throwOnVerifyContributorUsername,
                addContributorResult,
                throwOnAddContributor,
                deleteContributorResult,
                throwOnDeleteContributor),
            new FakeProfileSectionsApiService(questionnaireMetadata),
            NullLogger<MaintainContributorsModel>.Instance)
        {
            ProfileId = ProfileId,
            PageContext = new PageContext
            {
                HttpContext = httpContext,
                ViewData = new ViewDataDictionary(modelMetadataProvider, new ModelStateDictionary())
            },
            MetadataProvider = modelMetadataProvider
        };

        return pageModel;
    }

    private static ManageProfileViewModel Profile() => new()
    {
        ProfileId = ProfileId,
        ProfileTitle = "African Horse Sickness (AHS)"
    };

    private static ContributorDto Contributor(string userName = "carrie.batten") => new()
    {
        Id = Guid.NewGuid(),
        UserName = userName,
        FullName = "Carrie Batten",
        Organisation = "Pirbright Institute",
        Role = "Technical author"
    };

    private static ContributorEditDto ContributorEdit(Guid contributorId, Guid roleId, Guid sectionId) => new()
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

    private static ProfileQuestionnaireMetadataDto QuestionnaireMetadata(Guid sectionId) => new()
    {
        Sections = [new ProfileSectionMetadataDto { Id = sectionId, Name = "Epidemiology", ShortName = "Epi", SectionNumber = 1 }]
    };

    [Fact]
    public async Task OnGetAsync_PopulatesProfileTitleAndContributors_WhenTheProfileExists()
    {
        var contributors = new PagedResult<ContributorDto>
        {
            Items = [Contributor()],
            PageNumber = 1,
            PageSize = 10,
            TotalRecords = 1
        };
        var pageModel = CreatePageModel(Profile(), contributors: contributors);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("African Horse Sickness (AHS)", pageModel.ProfileTitle);
        Assert.False(pageModel.HasError);
        Assert.Single(pageModel.Contributors);
        Assert.Equal("carrie.batten", pageModel.Contributors[0].UserName);
        Assert.Equal(1, pageModel.TotalRecords);
        Assert.Equal(1, pageModel.TotalPages);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenNoProfileExists()
    {
        var pageModel = CreatePageModel(manageProfile: null);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.False(pageModel.HasError);
    }

    [Fact]
    public async Task OnGetAsync_SetsHasError_WhenLoadingTheProfileTitleFails()
    {
        var pageModel = CreatePageModel(throwOnGetManageProfile: new HttpRequestException("boom"));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasError);
    }

    [Fact]
    public async Task OnGetAsync_SetsHasError_WhenLoadingContributorsFails()
    {
        var pageModel = CreatePageModel(Profile(), throwOnGetProfileContributors: new HttpRequestException("boom"));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasError);
        Assert.Empty(pageModel.Contributors);
    }

    [Fact]
    public async Task OnGetAsync_PopulatesTheEditPanel_WhenEditContributorIdIsSet()
    {
        var contributorId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var editingContributor = ContributorEdit(contributorId, roleId, sectionId);
        var roles = new List<ProfileUserRoleDto> { new() { Id = roleId, Name = "Technical author", IsContributor = true } };
        var pageModel = CreatePageModel(
            Profile(),
            contributorForEdit: editingContributor,
            profileUserRoles: roles,
            questionnaireMetadata: QuestionnaireMetadata(sectionId));
        pageModel.EditContributorId = contributorId;

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Same(editingContributor, pageModel.EditingContributor);
        Assert.Single(pageModel.ProfileUserRoles);
        Assert.Single(pageModel.PermissionSections);
        Assert.Equal(contributorId, pageModel.ContributorId);
        Assert.Equal(roleId, pageModel.RoleId);
        Assert.Equal("Carrie Batten", pageModel.FullName);
        Assert.Equal("Pirbright Institute", pageModel.Organisation);
        Assert.Equal([sectionId], pageModel.SelectedSectionIds);
        Assert.True(pageModel.IsCurrentRoleContributor);
    }

    [Fact]
    public async Task OnGetAsync_ClosesTheEditPanel_WhenTheContributorIsNotFound()
    {
        var pageModel = CreatePageModel(Profile(), contributorForEdit: null);
        pageModel.EditContributorId = Guid.NewGuid();

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Null(pageModel.EditContributorId);
        Assert.Null(pageModel.EditingContributor);
    }

    [Fact]
    public async Task OnGetAsync_SetsHasError_WhenLoadingTheEditPanelFails()
    {
        var pageModel = CreatePageModel(Profile(), throwOnGetContributorForEdit: new HttpRequestException("boom"));
        pageModel.EditContributorId = Guid.NewGuid();

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasError);
    }

    [Fact]
    public async Task OnPostSaveContributorAsync_RedirectsWithASuccessMessage_WhenTheSaveSucceeds()
    {
        var contributorId = Guid.NewGuid();
        var pageModel = CreatePageModel(
            Profile(),
            updateContributorResult: new UpdateContributorResult { Outcome = ContributorUpdateOutcome.Success });
        pageModel.ContributorId = contributorId;
        pageModel.RoleId = Guid.NewGuid();
        pageModel.FullName = "Carrie Batten";
        pageModel.Organisation = "Pirbright Institute";
        pageModel.PageNumber = 2;

        var result = await pageModel.OnPostSaveContributorAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/SurveillanceProfiles/MaintainContributors", redirect.PageName);
        Assert.Equal("Your changes were successfully saved", pageModel.SuccessMessage);
    }

    [Fact]
    public async Task OnPostSaveContributorAsync_RedisplaysThePanel_WhenTheSaveFailsValidation()
    {
        var contributorId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var pageModel = CreatePageModel(
            Profile(),
            contributorForEdit: ContributorEdit(contributorId, roleId, sectionId),
            profileUserRoles: [new ProfileUserRoleDto { Id = roleId, Name = "Technical author", IsContributor = true }],
            questionnaireMetadata: QuestionnaireMetadata(sectionId),
            updateContributorResult: new UpdateContributorResult
            {
                Outcome = ContributorUpdateOutcome.ValidationFailed,
                ErrorMessage = "Please select a valid role."
            });
        pageModel.ContributorId = contributorId;
        pageModel.FullName = "Submitted Name";

        var result = await pageModel.OnPostSaveContributorAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(contributorId, pageModel.EditContributorId);
        Assert.Equal(["Please select a valid role."], pageModel.EditValidationErrors);
        Assert.Equal("Submitted Name", pageModel.FullName);
    }

    [Fact]
    public async Task OnPostSaveContributorAsync_SetsHasError_WhenTheApiThrows()
    {
        var pageModel = CreatePageModel(Profile(), throwOnUpdateContributor: new HttpRequestException("boom"));

        var result = await pageModel.OnPostSaveContributorAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasError);
    }

    [Fact]
    public async Task OnPostSaveContributorAsync_ReturnsNotFound_WhenNoProfileExists()
    {
        var pageModel = CreatePageModel(manageProfile: null);

        var result = await pageModel.OnPostSaveContributorAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnPostLookupUserAsync_ShowsAnError_WhenTheUserIsBlocked()
    {
        var pageModel = CreatePageModel(
            Profile(), verificationResult: new UserVerificationResultDto { Outcome = UserVerificationOutcome.Blocked });
        pageModel.LookupUserName = "internal\\admin";

        var result = await pageModel.OnPostLookupUserAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.AddContributor);
        Assert.Null(pageModel.VerifiedUser);
        Assert.Equal("This user cannot be made a contributor.", pageModel.LookupErrorMessage);
    }

    [Fact]
    public async Task OnPostLookupUserAsync_ShowsAnError_WhenTheUsernameIsInvalid()
    {
        var pageModel = CreatePageModel(
            Profile(), verificationResult: new UserVerificationResultDto { Outcome = UserVerificationOutcome.InvalidFormat });
        pageModel.LookupUserName = "not-valid";

        var result = await pageModel.OnPostLookupUserAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Null(pageModel.VerifiedUser);
        Assert.Equal("This user is not currently in the profiles system.", pageModel.LookupErrorMessage);
    }

    [Fact]
    public async Task OnPostLookupUserAsync_ShowsAWarningAndStep2_WhenTheUsernameIsNew()
    {
        var sectionId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var pageModel = CreatePageModel(
            Profile(),
            verificationResult: new UserVerificationResultDto { Outcome = UserVerificationOutcome.NewUser },
            profileUserRoles: [new ProfileUserRoleDto { Id = roleId, Name = "Technical author", IsContributor = true }],
            questionnaireMetadata: QuestionnaireMetadata(sectionId));
        pageModel.LookupUserName = "internal\\new.user";

        var result = await pageModel.OnPostLookupUserAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(pageModel.VerifiedUser);
        Assert.Equal(UserVerificationOutcome.NewUser, pageModel.VerifiedUser!.Outcome);
        Assert.True(pageModel.IsNewUser);
        Assert.False(pageModel.IsSsoUser);
        Assert.Equal("The user is not currently in the profiles system.", pageModel.LookupWarningMessage);
        Assert.Single(pageModel.ProfileUserRoles);
    }

    [Fact]
    public async Task OnPostLookupUserAsync_LoadsExistingUserDetail_WhenTheUsernameMatchesAGlobalUser()
    {
        var contributorId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var pageModel = CreatePageModel(
            Profile(),
            verificationResult: new UserVerificationResultDto { Outcome = UserVerificationOutcome.ExistingUser, UserId = contributorId },
            contributorForEdit: ContributorEdit(contributorId, roleId, sectionId),
            profileUserRoles: [new ProfileUserRoleDto { Id = roleId, Name = "Technical author", IsContributor = true }],
            questionnaireMetadata: QuestionnaireMetadata(sectionId));
        pageModel.LookupUserName = "internal\\carrie.batten";

        var result = await pageModel.OnPostLookupUserAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.NotNull(pageModel.VerifiedUser);
        Assert.Equal(contributorId, pageModel.ContributorId);
        Assert.Equal("carrie.batten", pageModel.LookupUserName);
        Assert.Equal(roleId, pageModel.RoleId);
        Assert.False(pageModel.IsNewUser);
    }

    [Fact]
    public async Task OnPostLookupUserAsync_SetsHasError_WhenTheApiThrows()
    {
        var pageModel = CreatePageModel(Profile(), throwOnVerifyContributorUsername: new HttpRequestException("boom"));
        pageModel.LookupUserName = "internal\\carrie.batten";

        var result = await pageModel.OnPostLookupUserAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasError);
    }

    [Fact]
    public async Task OnPostAddContributorAsync_RedirectsWithASuccessMessage_WhenTheAddSucceeds()
    {
        var pageModel = CreatePageModel(
            Profile(), addContributorResult: new AddContributorResult { Outcome = ContributorAddOutcome.Success });
        pageModel.ContributorId = Guid.NewGuid();
        pageModel.LookupUserName = "internal\\new.user";
        pageModel.RoleId = Guid.NewGuid();
        pageModel.FullName = "New User";
        pageModel.Organisation = "Pirbright Institute";

        var result = await pageModel.OnPostAddContributorAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/SurveillanceProfiles/MaintainContributors", redirect.PageName);
        Assert.Equal("The contributor was successfully added", pageModel.SuccessMessage);
    }

    [Fact]
    public async Task OnPostAddContributorAsync_RedisplaysStep2_WhenTheAddFailsValidation()
    {
        var roleId = Guid.NewGuid();
        var pageModel = CreatePageModel(
            Profile(),
            profileUserRoles: [new ProfileUserRoleDto { Id = roleId, Name = "Technical author", IsContributor = true }],
            addContributorResult: new AddContributorResult
            {
                Outcome = ContributorAddOutcome.ValidationFailed,
                ErrorMessage = "Please select a valid role."
            });
        pageModel.ContributorId = Guid.NewGuid();
        pageModel.LookupUserName = "internal\\new.user";
        pageModel.IsNewUser = true;

        var result = await pageModel.OnPostAddContributorAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.AddContributor);
        Assert.NotNull(pageModel.VerifiedUser);
        Assert.Equal(["Please select a valid role."], pageModel.AddValidationErrors);
    }

    [Fact]
    public async Task OnPostAddContributorAsync_SetsHasError_WhenTheApiThrows()
    {
        var pageModel = CreatePageModel(Profile(), throwOnAddContributor: new HttpRequestException("boom"));

        var result = await pageModel.OnPostAddContributorAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasError);
    }

    [Fact]
    public async Task OnPostAddContributorAsync_ReturnsNotFound_WhenNoProfileExists()
    {
        var pageModel = CreatePageModel(manageProfile: null);

        var result = await pageModel.OnPostAddContributorAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task OnPostDeleteContributorAsync_RedirectsWithASuccessMessage_WhenTheDeleteSucceeds()
    {
        var pageModel = CreatePageModel(
            Profile(), deleteContributorResult: new DeleteContributorResult { Outcome = ContributorDeleteOutcome.Success });
        pageModel.ContributorId = Guid.NewGuid();
        pageModel.LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8];

        var result = await pageModel.OnPostDeleteContributorAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/SurveillanceProfiles/MaintainContributors", redirect.PageName);
        Assert.Equal("The user was successfully removed from the list of contributors", pageModel.SuccessMessage);
    }

    [Fact]
    public async Task OnPostDeleteContributorAsync_RedirectsWithAnErrorMessage_WhenTheDeleteConflicts()
    {
        var pageModel = CreatePageModel(
            Profile(),
            deleteContributorResult: new DeleteContributorResult
            {
                Outcome = ContributorDeleteOutcome.Conflict,
                ErrorMessage = "Another user has changed this contributor since the page was loaded. Reload and try again."
            });
        pageModel.ContributorId = Guid.NewGuid();
        pageModel.LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8];

        var result = await pageModel.OnPostDeleteContributorAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(pageModel.SuccessMessage);
        Assert.Equal(
            "Another user has changed this contributor since the page was loaded. Reload and try again.",
            pageModel.DeleteErrorMessage);
    }

    [Fact]
    public async Task OnPostDeleteContributorAsync_RedirectsWithAnErrorMessage_WhenTheApiThrows()
    {
        var pageModel = CreatePageModel(Profile(), throwOnDeleteContributor: new HttpRequestException("boom"));
        pageModel.ContributorId = Guid.NewGuid();
        pageModel.LastUpdated = [1, 2, 3, 4, 5, 6, 7, 8];

        var result = await pageModel.OnPostDeleteContributorAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("We could not remove this contributor. Try again later.", pageModel.DeleteErrorMessage);
    }

    [Theory]
    [InlineData("All", 0)]
    [InlineData("20", 20)]
    [InlineData("not-a-number", 10)]
    public async Task OnGetAsync_ResolvesPageSize_FromTheItemsPerPageOption(string pageSizeOption, int expectedPageSize)
    {
        var modelMetadataProvider = new EmptyModelMetadataProvider();
        var httpContext = new DefaultHttpContext();
        var fakeContributorsApiService = new FakeProfileContributorsApiService();
        var pageModel = new MaintainContributorsModel(
            new FakeApiClient(manageProfile: Profile()),
            fakeContributorsApiService,
            new FakeProfileSectionsApiService(),
            NullLogger<MaintainContributorsModel>.Instance)
        {
            ProfileId = ProfileId,
            PageSize = pageSizeOption,
            PageContext = new PageContext
            {
                HttpContext = httpContext,
                ViewData = new ViewDataDictionary(modelMetadataProvider, new ModelStateDictionary())
            },
            MetadataProvider = modelMetadataProvider
        };

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(expectedPageSize, fakeContributorsApiService.RequestedPageSize);
    }
}
