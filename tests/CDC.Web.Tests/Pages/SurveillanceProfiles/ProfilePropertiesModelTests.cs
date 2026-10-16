using CDC.Web.Models;
using CDC.Web.Pages.SurveillanceProfiles;
using CDC.Web.Tests.Features.Landing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace CDC.Web.Tests.Pages.SurveillanceProfiles;

public class ProfilePropertiesModelTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly Guid DraftVersionId = Guid.NewGuid();
    private static readonly Guid CloneVersionId = Guid.NewGuid();
    private static readonly Guid ParentId = Guid.NewGuid();
    private static readonly Guid StatusId = Guid.NewGuid();
    private static readonly byte[] RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];

    private static readonly Guid Animals = Guid.NewGuid();
    private static readonly Guid Cattle = Guid.NewGuid();
    private static readonly Guid Bulls = Guid.NewGuid();
    private static readonly Guid Sheep = Guid.NewGuid();
    private static readonly Guid Goats = Guid.NewGuid();

    private static readonly IReadOnlyList<SpeciesDto> Species =
    [
        new() { Id = Animals, ParentId = Guid.Empty, Description = "Animals", IsActive = true },
        new() { Id = Cattle, ParentId = Animals, Description = "Cattle", IsActive = true },
        new() { Id = Bulls, ParentId = Cattle, Description = "Bulls", IsActive = true },
        new() { Id = Sheep, ParentId = Animals, Description = "Sheep", IsActive = true },
        new() { Id = Goats, ParentId = Animals, Description = "Goats", IsActive = false }
    ];

    private static ProfilePropertiesModel CreatePageModel(
        ProfileAttributesDto? attributes = null,
        Exception? throwOnGetProfileAttributes = null,
        FakeProfilePropertiesApiService? properties = null,
        Exception? throwOnGetSpecies = null,
        Guid? profileId = null,
        Guid? cloneProfileVersionId = null,
        string? scenario = null)
    {
        var modelMetadataProvider = new EmptyModelMetadataProvider();

        return new ProfilePropertiesModel(
            new FakeApiClient(profileAttributes: attributes, throwOnGetProfileAttributes: throwOnGetProfileAttributes),
            properties ?? new FakeProfilePropertiesApiService(),
            new FakeSpeciesApiService(species: Species, throwOnGetAllSpecies: throwOnGetSpecies),
            new AlwaysEnabledLogger<ProfilePropertiesModel>())
        {
            ProfileId = profileId,
            CloneProfileVersionId = cloneProfileVersionId,
            Scenario = scenario,
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext(),
                ViewData = new ViewDataDictionary(modelMetadataProvider, new ModelStateDictionary())
            },
            MetadataProvider = modelMetadataProvider
        };
    }

    private static ProfileAttributesDto Profile(
        string title = "Bovine tuberculosis",
        Guid? draftVersionId = null,
        IReadOnlyList<AffectedSpeciesDto>? affectedSpecies = null) => new()
        {
            Id = ProfileId,
            Title = title,
            CurrentDraftProfileVersionId = draftVersionId ?? DraftVersionId,
            LastUpdated = RowVersion,
            AffectedSpecies = affectedSpecies ?? [Affected(Sheep, "Profiled"), Affected(Goats, "Other")]
        };

    private static ProfileAttributesDto Scenario(Guid? draftVersionId = null) => new()
    {
        Id = ProfileId,
        ParentId = ParentId,
        ParentTitle = "Bovine tuberculosis",
        ScenarioTitle = "Badger cull",
        CurrentDraftProfileVersionId = draftVersionId ?? DraftVersionId,
        LastUpdated = RowVersion,
        AffectedSpecies = [Affected(Cattle, "Profiled")]
    };

    private static AffectedSpeciesDto Affected(Guid id, string type) =>
        new() { SpeciesId = id, Name = "species", Type = type, IsActive = true };

    private static NewProfileDefaultsDto Defaults(bool clonedVersionIsScenario = false) => new()
    {
        Title = "Bovine tuberculosis",
        ScenarioTitle = "Badger cull",
        ParentId = ParentId,
        ParentTitle = "Bovine tuberculosis",
        ProfileStatusId = StatusId,
        ClonedVersionDescription = "Bovine tuberculosis - v1.0 (Published) Valid from 01/01/2020",
        ClonedVersionIsWhatIfScenario = clonedVersionIsScenario,
        AffectedSpecies = [Affected(Cattle, "Profiled"), Affected(Sheep, "Other")]
    };

    private static List<string> Errors(ProfilePropertiesModel pageModel, string key) =>
        [.. pageModel.ModelState[key]?.Errors.Select(error => error.ErrorMessage) ?? []];

    // ---- Opening the page ----

    [Fact]
    public async Task OnGetAsync_OpensABlankForm_WhenCreatingAProfile()
    {
        var pageModel = CreatePageModel();

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(ProfilePropertiesMode.Create, pageModel.Mode);
        Assert.Equal("Create new profile", pageModel.Heading);
        Assert.Equal("Create profile", pageModel.SubmitButtonText);
        Assert.Equal("Profile title", pageModel.TitleLabel);
        Assert.True(pageModel.ShowForm);
        Assert.True(pageModel.ShowSpeciesSelector);
        Assert.True(pageModel.ShowInfoPanel);
        Assert.False(pageModel.ShowRemovalWarning);
        Assert.Null(pageModel.ManageProfileId);
        Assert.Null(pageModel.ClonedFromDescription);
        Assert.Null(pageModel.ProfileTitle);
        Assert.Empty(pageModel.ProfiledSpeciesIds);
        Assert.Single(pageModel.ProfiledSpeciesTree.Nodes);
    }

    [Fact]
    public async Task OnGetAsync_TreatsAnEmptyCloneIdAsCreatingAProfile()
    {
        var properties = new FakeProfilePropertiesApiService();
        var pageModel = CreatePageModel(properties: properties, cloneProfileVersionId: Guid.Empty);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(ProfilePropertiesMode.Create, pageModel.Mode);
        Assert.Null(properties.LastDefaultsRequest);
    }

    [Fact]
    public async Task OnGetAsync_PrefillsTheForm_WhenCloningAProfile()
    {
        var properties = new FakeProfilePropertiesApiService(defaults: Defaults());
        var pageModel = CreatePageModel(properties: properties, cloneProfileVersionId: CloneVersionId);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal((CloneVersionId, false), properties.LastDefaultsRequest);
        Assert.Equal(ProfilePropertiesMode.CloneProfile, pageModel.Mode);
        Assert.Equal("Clone new profile", pageModel.Heading);
        Assert.Equal("Bovine tuberculosis", pageModel.ProfileTitle);
        Assert.Equal("Bovine tuberculosis - v1.0 (Published) Valid from 01/01/2020", pageModel.ClonedFromDescription);
        Assert.Equal([Cattle], pageModel.ProfiledSpeciesIds);
        Assert.Equal([Sheep], pageModel.OtherSpeciesIds);
        Assert.Contains(Cattle.ToString(), pageModel.ProfiledSpeciesTree.SelectedValues);
        Assert.Contains(Sheep.ToString(), pageModel.OtherSpeciesTree.SelectedValues);
        Assert.False(pageModel.IsScenarioTitle);
        Assert.True(pageModel.ShowInfoPanel);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("TRUE")]
    public async Task OnGetAsync_UsesTheScenarioTitle_WhenCloningAScenario(string scenario)
    {
        var properties = new FakeProfilePropertiesApiService(defaults: Defaults());
        var pageModel = CreatePageModel(properties: properties, cloneProfileVersionId: CloneVersionId, scenario: scenario);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal((CloneVersionId, true), properties.LastDefaultsRequest);
        Assert.Equal(ProfilePropertiesMode.CloneScenario, pageModel.Mode);
        Assert.Equal("Clone new \"what-if\" scenario for Bovine tuberculosis", pageModel.Heading);
        Assert.Equal("Scenario title", pageModel.TitleLabel);
        Assert.Equal("Badger cull", pageModel.ProfileTitle);
        Assert.Equal("Clone scenario", pageModel.BreadcrumbCurrent);
    }

    [Fact]
    public async Task OnGetAsync_IgnoresAnUnrecognisedScenarioFlag()
    {
        var pageModel = CreatePageModel(
            properties: new FakeProfilePropertiesApiService(defaults: Defaults()),
            cloneProfileVersionId: CloneVersionId,
            scenario: "maybe");

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(ProfilePropertiesMode.CloneProfile, pageModel.Mode);
    }

    [Fact]
    public async Task OnGetAsync_RefusesToCloneAProfileFromAScenarioVersion()
    {
        var pageModel = CreatePageModel(
            properties: new FakeProfilePropertiesApiService(defaults: Defaults(clonedVersionIsScenario: true)),
            cloneProfileVersionId: CloneVersionId);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ShowForm);
        Assert.Contains(Errors(pageModel, string.Empty), message => message.StartsWith("You cannot clone a new \"current situation\" profile", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheCloneSourceDoesNotExist()
    {
        var pageModel = CreatePageModel(
            properties: new FakeProfilePropertiesApiService(defaults: null),
            cloneProfileVersionId: CloneVersionId);

        Assert.IsType<NotFoundResult>(await pageModel.OnGetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnGetAsync_PopulatesTheForm_WhenEditingAProfile()
    {
        var pageModel = CreatePageModel(Profile(), profileId: ProfileId);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(ProfilePropertiesMode.Edit, pageModel.Mode);
        Assert.Equal("Profile properties for Bovine tuberculosis", pageModel.Heading);
        Assert.Equal("Update", pageModel.SubmitButtonText);
        Assert.Equal("Bovine tuberculosis", pageModel.ProfileTitle);
        Assert.Equal(Convert.ToBase64String(RowVersion), pageModel.LastUpdated);
        Assert.Equal([Sheep], pageModel.ProfiledSpeciesIds);
        Assert.Equal([Goats], pageModel.OtherSpeciesIds);
        Assert.True(pageModel.ShowSpeciesSelector);
        Assert.True(pageModel.ShowRemovalWarning);
        Assert.False(pageModel.ShowInfoPanel);
        Assert.Equal(ProfileId, pageModel.ManageProfileId);
        Assert.Equal("Profile properties", pageModel.BreadcrumbCurrent);
    }

    [Fact]
    public async Task OnGetAsync_ShowsEachSpeciesOnceInBothTrees_WithTheInactiveOnesMarked()
    {
        var pageModel = CreatePageModel(Profile(), profileId: ProfileId);

        await pageModel.OnGetAsync(CancellationToken.None);

        var animals = Assert.Single(pageModel.ProfiledSpeciesTree.Nodes);
        Assert.True(animals.Expanded);
        Assert.Equal(["Cattle", "Goats", "Sheep"], animals.Children.Select(child => child.Label));
        Assert.True(animals.Children.Single(child => child.Label == "Goats").IsInactive);
        Assert.Equal(pageModel.ProfiledSpeciesTree.Nodes, pageModel.OtherSpeciesTree.Nodes);
        Assert.All(animals.Children.Where(child => child.Children.Count > 0), child => Assert.True(child.Expanded));
        Assert.Equal("ProfiledSpeciesIds", pageModel.ProfiledSpeciesTree.FieldName);
        Assert.Equal("OtherSpeciesIds", pageModel.OtherSpeciesTree.FieldName);
    }

    [Fact]
    public async Task OnGetAsync_ShowsOnlyTheTopMostSpeciesOfAStoredList()
    {
        var pageModel = CreatePageModel(
            Profile(affectedSpecies: [Affected(Bulls, "Profiled"), Affected(Cattle, "Profiled")]),
            profileId: ProfileId);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal([Cattle], pageModel.ProfiledSpeciesIds);
    }

    [Fact]
    public async Task OnGetAsync_EditsTheScenarioTitle_WhenEditingAScenario()
    {
        var pageModel = CreatePageModel(Scenario(), profileId: ProfileId);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.IsScenarioTitle);
        Assert.Equal("Scenario title", pageModel.TitleLabel);
        Assert.Equal("Badger cull", pageModel.ProfileTitle);
        Assert.Equal("Profile properties for Bovine tuberculosis (Badger cull)", pageModel.Heading);
    }

    [Fact]
    public async Task OnGetAsync_HidesTheSpeciesAndDoesNotLoadThem_WhenAScenarioHasNoDraft()
    {
        var pageModel = CreatePageModel(Scenario(draftVersionId: Guid.Empty), profileId: ProfileId);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.ShowForm);
        Assert.False(pageModel.ShowSpeciesSelector);
        Assert.False(pageModel.ShowRemovalWarning);
        Assert.Empty(pageModel.ProfiledSpeciesTree.Nodes);
    }

    [Fact]
    public async Task OnGetAsync_ExplainsThereIsNothingToEdit_WhenAProfileHasNoDraft()
    {
        var pageModel = CreatePageModel(Profile(draftVersionId: Guid.Empty), profileId: ProfileId);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ShowForm);
        Assert.Equal(["There are no editable properties for this profile."], Errors(pageModel, string.Empty));
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheProfileDoesNotExist()
    {
        var pageModel = CreatePageModel(attributes: null, profileId: ProfileId);

        Assert.IsType<NotFoundResult>(await pageModel.OnGetAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnGetAsync_ShowsAnError_WhenTheProfileCannotBeLoaded()
    {
        var pageModel = CreatePageModel(throwOnGetProfileAttributes: new HttpRequestException("down"), profileId: ProfileId);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ShowForm);
        Assert.Equal(["We could not load this page. Try again later."], Errors(pageModel, string.Empty));
    }

    [Fact]
    public async Task OnGetAsync_ShowsAnError_WhenTheSpeciesCannotBeLoaded()
    {
        var pageModel = CreatePageModel(throwOnGetSpecies: new HttpRequestException("down"));

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.False(pageModel.ShowForm);
        Assert.Equal(["We could not load this page. Try again later."], Errors(pageModel, string.Empty));
    }

    [Fact]
    public async Task OnGetAsync_ShowsAnError_WhenTheCloneDefaultsCannotBeLoaded()
    {
        var pageModel = CreatePageModel(
            properties: new FakeProfilePropertiesApiService(throwOnGetDefaults: new TaskCanceledException()),
            cloneProfileVersionId: CloneVersionId);

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.False(pageModel.ShowForm);
    }

    // ---- Validating a post ----

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<p><br></p>")]
    [InlineData("&nbsp;")]
    public async Task OnPostAsync_RequiresATitle(string? title)
    {
        var properties = new FakeProfilePropertiesApiService();
        var pageModel = CreatePageModel(properties: properties);
        pageModel.ProfileTitle = title;
        pageModel.ProfiledSpeciesIds = [Sheep];

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(["You must enter a profile title"], Errors(pageModel, nameof(ProfilePropertiesModel.ProfileTitle)));
        Assert.Null(properties.LastCreateRequest);
    }

    [Fact]
    public async Task OnPostAsync_RequiresAScenarioTitle_WhenCloningAScenario()
    {
        var pageModel = CreatePageModel(
            properties: new FakeProfilePropertiesApiService(defaults: Defaults()),
            cloneProfileVersionId: CloneVersionId,
            scenario: "1");
        pageModel.ProfileTitle = " ";
        pageModel.ProfiledSpeciesIds = [Sheep];

        await pageModel.OnPostAsync(CancellationToken.None);

        Assert.Equal(["You must enter a scenario title"], Errors(pageModel, nameof(ProfilePropertiesModel.ProfileTitle)));
    }

    [Fact]
    public async Task OnPostAsync_RejectsATitleOverTheLimit()
    {
        var pageModel = CreatePageModel();
        pageModel.ProfileTitle = new string('a', 256);
        pageModel.ProfiledSpeciesIds = [Sheep];

        await pageModel.OnPostAsync(CancellationToken.None);

        Assert.Equal(["Profile title cannot exceed 255 characters"], Errors(pageModel, nameof(ProfilePropertiesModel.ProfileTitle)));
    }

    [Fact]
    public async Task OnPostAsync_RequiresAProfiledSpecies_AndKeepsWhatWasPicked()
    {
        var pageModel = CreatePageModel();
        pageModel.ProfileTitle = "Anthrax";
        pageModel.OtherSpeciesIds = [Sheep];

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal([AffectedSpeciesSelection.NoProfiledSpeciesMessage], Errors(pageModel, "ProfiledSpeciesIds"));
        Assert.Contains(Sheep.ToString(), pageModel.OtherSpeciesTree.SelectedValues);
    }

    [Fact]
    public async Task OnPostAsync_RejectsASpeciesPickedAsBothTypes()
    {
        var pageModel = CreatePageModel();
        pageModel.ProfileTitle = "Anthrax";
        pageModel.ProfiledSpeciesIds = [Sheep];
        pageModel.OtherSpeciesIds = [Sheep];

        await pageModel.OnPostAsync(CancellationToken.None);

        Assert.Single(Errors(pageModel, "ProfiledSpeciesIds"));
    }

    [Fact]
    public async Task OnPostAsync_RejectsANewlyPickedInactiveSpecies_ButAllowsOneTheProfileAlreadyHas()
    {
        var rejected = CreatePageModel();
        rejected.ProfileTitle = "Anthrax";
        rejected.ProfiledSpeciesIds = [Sheep];
        rejected.OtherSpeciesIds = [Goats];

        await rejected.OnPostAsync(CancellationToken.None);

        Assert.Equal(["Goats is inactive and cannot be selected"], Errors(rejected, "ProfiledSpeciesIds"));

        var properties = new FakeProfilePropertiesApiService();
        var allowed = CreatePageModel(Profile(), properties: properties, profileId: ProfileId);
        allowed.ProfileTitle = "Anthrax";
        allowed.LastUpdated = Convert.ToBase64String(RowVersion);
        allowed.ProfiledSpeciesIds = [Sheep];
        allowed.OtherSpeciesIds = [Goats];

        Assert.IsType<RedirectToPageResult>(await allowed.OnPostAsync(CancellationToken.None));
    }

    [Fact]
    public async Task OnPostAsync_ShowsTheErrorPage_WhenTheProfileCannotBeLoaded()
    {
        var pageModel = CreatePageModel(throwOnGetSpecies: new HttpRequestException("down"));
        pageModel.ProfileTitle = "Anthrax";

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ShowForm);
    }

    [Fact]
    public async Task OnPostAsync_ReturnsNotFound_WhenTheProfileHasGone()
    {
        var pageModel = CreatePageModel(attributes: null, profileId: ProfileId);

        Assert.IsType<NotFoundResult>(await pageModel.OnPostAsync(CancellationToken.None));
    }

    // ---- Creating ----

    [Fact]
    public async Task OnPostAsync_CreatesTheProfile_AndGoesOnToItsQuestions()
    {
        var properties = new FakeProfilePropertiesApiService();
        var pageModel = CreatePageModel(properties: properties);
        pageModel.ProfileTitle = "  <em>Anthrax</em>  ";
        pageModel.ProfiledSpeciesIds = [Bulls, Cattle];
        pageModel.OtherSpeciesIds = [Sheep];

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        var request = properties.LastCreateRequest!;
        Assert.Equal("/SurveillanceProfiles/EditProfileQuestions", redirect.PageName);
        Assert.Equal(request.Id, redirect.RouteValues!["profileId"]);
        Assert.Equal("<em>Anthrax</em>", request.Title);
        Assert.Equal(string.Empty, request.ScenarioTitle);
        Assert.Equal(Guid.Empty, request.ParentId);
        Assert.Equal(Guid.Empty, request.CloneProfileVersionId);
        Assert.Equal(Guid.Empty, request.ProfileStatusId);
        Assert.NotEqual(Guid.Empty, request.CurrentDraftProfileVersionId);
        Assert.Collection(
            request.AffectedSpeciesInsertList,
            first => Assert.Equal((request.CurrentDraftProfileVersionId, Guid.Empty, Cattle, "Profiled"), (first.ProfileVersionId, first.CloneProfileVersionId, first.SpeciesId, first.Type)),
            second => Assert.Equal((request.CurrentDraftProfileVersionId, Guid.Empty, Sheep, "Other"), (second.ProfileVersionId, second.CloneProfileVersionId, second.SpeciesId, second.Type)));
    }

    [Fact]
    public async Task OnPostAsync_CreatesAProfileClonedFromAVersion()
    {
        var properties = new FakeProfilePropertiesApiService(defaults: Defaults());
        var pageModel = CreatePageModel(properties: properties, cloneProfileVersionId: CloneVersionId);
        pageModel.ProfileTitle = "Bovine tuberculosis (copy)";
        pageModel.ProfiledSpeciesIds = [Cattle];

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        var request = properties.LastCreateRequest!;
        Assert.Equal(CloneVersionId, request.CloneProfileVersionId);
        Assert.Equal("Bovine tuberculosis (copy)", request.Title);
        Assert.Equal(string.Empty, request.ScenarioTitle);
        Assert.Equal(Guid.Empty, request.ParentId);
        Assert.Equal(StatusId, request.ProfileStatusId);
        var insert = Assert.Single(request.AffectedSpeciesInsertList);
        Assert.Equal(CloneVersionId, insert.CloneProfileVersionId);
    }

    [Fact]
    public async Task OnPostAsync_CreatesAScenarioBelongingToTheParentProfile()
    {
        var properties = new FakeProfilePropertiesApiService(defaults: Defaults());
        var pageModel = CreatePageModel(properties: properties, cloneProfileVersionId: CloneVersionId, scenario: "1");
        pageModel.ProfileTitle = "Wildlife outbreak";
        pageModel.ProfiledSpeciesIds = [Cattle];

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        var request = properties.LastCreateRequest!;
        Assert.Equal(string.Empty, request.Title);
        Assert.Equal("Wildlife outbreak", request.ScenarioTitle);
        Assert.Equal(ParentId, request.ParentId);
        Assert.Equal("Bovine tuberculosis", request.ParentTitle);
        Assert.Equal(CloneVersionId, request.CloneProfileVersionId);
        Assert.Equal(StatusId, request.ProfileStatusId);
    }

    [Theory]
    [InlineData(SaveProfileOutcome.ValidationFailed)]
    [InlineData(SaveProfileOutcome.Conflict)]
    [InlineData(SaveProfileOutcome.Error)]
    public async Task OnPostAsync_ShowsWhyTheProfileWasNotCreated(SaveProfileOutcome outcome)
    {
        var pageModel = CreatePageModel(properties: new FakeProfilePropertiesApiService(createResult: new CreateProfileResult(outcome, null, "A title is required.")));
        pageModel.ProfileTitle = "Anthrax";
        pageModel.ProfiledSpeciesIds = [Sheep];

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(["Profile creation failed: A title is required."], Errors(pageModel, string.Empty));
        Assert.Contains(Sheep.ToString(), pageModel.ProfiledSpeciesTree.SelectedValues);
    }

    [Fact]
    public async Task OnPostAsync_ShowsAnError_WhenCreatingTheProfileFailsOnTheNetwork()
    {
        var pageModel = CreatePageModel(properties: new FakeProfilePropertiesApiService(throwOnSave: new HttpRequestException("down")));
        pageModel.ProfileTitle = "Anthrax";
        pageModel.ProfiledSpeciesIds = [Sheep];

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(["Profile creation failed: The profile could not be created. Please try again."], Errors(pageModel, string.Empty));
    }

    // ---- Updating ----

    private static ProfilePropertiesModel EditPageModel(
        FakeProfilePropertiesApiService properties,
        ProfileAttributesDto? attributes = null,
        string? lastUpdated = null)
    {
        var pageModel = CreatePageModel(attributes ?? Profile(), properties: properties, profileId: ProfileId);
        pageModel.LastUpdated = lastUpdated ?? Convert.ToBase64String(RowVersion);

        return pageModel;
    }

    [Fact]
    public async Task OnPostAsync_UpdatesTheProfile_AndReloadsThePageWithAMessage()
    {
        var properties = new FakeProfilePropertiesApiService();
        var pageModel = EditPageModel(properties);
        pageModel.ProfileTitle = " Bovine TB ";
        pageModel.ProfiledSpeciesIds = [Cattle];
        pageModel.OtherSpeciesIds = [Goats];

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Null(redirect.PageName);
        Assert.Equal(ProfileId, redirect.RouteValues!["profileId"]);
        Assert.Equal("Your changes were successfully saved", pageModel.StatusMessage);
        var (profileId, request) = properties.LastUpdate!.Value;
        Assert.Equal(ProfileId, profileId);
        Assert.Equal(ProfileId, request.Id);
        Assert.Equal("Bovine TB", request.Title);
        Assert.Equal(string.Empty, request.ScenarioTitle);
        Assert.Equal(RowVersion, request.LastUpdated);
        var delete = Assert.Single(request.AffectedSpeciesDeleteList);
        Assert.Equal((DraftVersionId, Sheep), (delete.ProfileVersionId, delete.SpeciesId));
        var insert = Assert.Single(request.AffectedSpeciesInsertList);
        Assert.Equal((DraftVersionId, Guid.Empty, Cattle, "Profiled"), (insert.ProfileVersionId, insert.CloneProfileVersionId, insert.SpeciesId, insert.Type));
    }

    [Fact]
    public async Task OnPostAsync_UpdatesAScenarioTitle_WithoutTouchingTheProfileTitle()
    {
        var properties = new FakeProfilePropertiesApiService();
        var pageModel = EditPageModel(properties, Scenario());
        pageModel.ProfileTitle = "Wildlife outbreak";
        pageModel.ProfiledSpeciesIds = [Cattle];

        await pageModel.OnPostAsync(CancellationToken.None);

        var request = properties.LastUpdate!.Value.Request;
        Assert.Equal(string.Empty, request.Title);
        Assert.Equal("Wildlife outbreak", request.ScenarioTitle);
        Assert.Empty(request.AffectedSpeciesInsertList);
        Assert.Empty(request.AffectedSpeciesDeleteList);
    }

    [Fact]
    public async Task OnPostAsync_ChangesOnlyTheTitle_WhenAScenarioHasNoDraft()
    {
        var properties = new FakeProfilePropertiesApiService();
        var pageModel = EditPageModel(properties, Scenario(draftVersionId: Guid.Empty));
        pageModel.ProfileTitle = "Wildlife outbreak";

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<RedirectToPageResult>(result);
        var request = properties.LastUpdate!.Value.Request;
        Assert.Equal("Wildlife outbreak", request.ScenarioTitle);
        Assert.Empty(request.AffectedSpeciesInsertList);
        Assert.Empty(request.AffectedSpeciesDeleteList);
    }

    [Fact]
    public async Task OnPostAsync_ReturnsToTheForm_WhenTheRowVersionIsNotValid()
    {
        var properties = new FakeProfilePropertiesApiService();
        var pageModel = EditPageModel(properties, lastUpdated: "not base64!");
        pageModel.ProfileTitle = "Bovine TB";
        pageModel.ProfiledSpeciesIds = [Sheep];

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(["The page has expired. Reload the profile and try again."], Errors(pageModel, string.Empty));
        Assert.Null(properties.LastUpdate);
    }

    [Fact]
    public async Task OnPostAsync_ReloadsWhatIsSaved_WhenAnotherUserChangedTheProfileFirst()
    {
        var properties = new FakeProfilePropertiesApiService(
            updateResult: new UpdateProfileAttributesResult(SaveProfileOutcome.Conflict, "This profile has been edited by another user."));
        var pageModel = EditPageModel(properties, Profile(title: "Saved by someone else"));
        pageModel.ProfileTitle = "My change";
        pageModel.ProfiledSpeciesIds = [Cattle];

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(["Profile update failed: This profile has been edited by another user."], Errors(pageModel, string.Empty));
        Assert.Equal("Saved by someone else", pageModel.ProfileTitle);
        Assert.Equal([Sheep], pageModel.ProfiledSpeciesIds);
        Assert.Null(pageModel.StatusMessage);
    }

    [Theory]
    [InlineData(SaveProfileOutcome.ValidationFailed)]
    [InlineData(SaveProfileOutcome.Error)]
    public async Task OnPostAsync_KeepsWhatWasTyped_WhenTheUpdateIsRefused(SaveProfileOutcome outcome)
    {
        var properties = new FakeProfilePropertiesApiService(updateResult: new UpdateProfileAttributesResult(outcome, "Nope."));
        var pageModel = EditPageModel(properties);
        pageModel.ProfileTitle = "My change";
        pageModel.ProfiledSpeciesIds = [Cattle];

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(["Profile update failed: Nope."], Errors(pageModel, string.Empty));
        Assert.Equal("My change", pageModel.ProfileTitle);
        Assert.Equal([Cattle], pageModel.ProfiledSpeciesIds);
    }

    [Fact]
    public async Task OnPostAsync_ShowsAnError_WhenUpdatingTheProfileFailsOnTheNetwork()
    {
        var properties = new FakeProfilePropertiesApiService(throwOnSave: new TaskCanceledException());
        var pageModel = EditPageModel(properties);
        pageModel.ProfileTitle = "Bovine TB";
        pageModel.ProfiledSpeciesIds = [Sheep];

        var result = await pageModel.OnPostAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal(["Profile update failed: The profile could not be saved. Please try again."], Errors(pageModel, string.Empty));
    }
}
