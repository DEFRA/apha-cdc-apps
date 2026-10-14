using CDC.Web.Models;
using CDC.Web.Pages.DiseaseRanking;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CDC.Web.Tests.Pages;

public class EditFiltersModelTests
{
    private static readonly Guid SpeciesId = Guid.Parse("6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f");

    private static readonly IReadOnlyList<SpeciesDto> Species =
    [
        new SpeciesDto { Id = SpeciesId, ParentId = Guid.Empty, Description = "Cattle", IsActive = true, IsInUse = true }
    ];

    private static readonly DiseaseRankingFilterDetailDto ExistingFilter = new()
    {
        FilterNumber = 1,
        DiseaseName = "Anthrax",
        Disease1 = "endemic diseases",
        Disease2 = "both",
        Disease3 = "both",
        Disease4 = "both",
        DiseaseTypes = ["bacteria", "virus"],
        SpeciesIds = [SpeciesId]
    };

    [Fact]
    public async Task OnGetAsync_SetsError_WhenFiltersFailToLoad()
    {
        var pageModel = CreatePageModel(
            new FakeDiseaseRankingFiltersApiService(throwOnGetFilters: new HttpRequestException("down")));

        await pageModel.OnGetAsync(created: false, saved: false, deleted: false, CancellationToken.None);

        Assert.True(pageModel.HasError);
        Assert.False(string.IsNullOrWhiteSpace(pageModel.ErrorMessage));
    }

    [Theory]
    [InlineData(true, false, false, "Filter value successfully saved")]
    [InlineData(false, true, false, "Filter value successfully updated")]
    [InlineData(false, false, true, "Filter value successfully deleted")]
    public async Task OnGetAsync_ShowsTheConfirmationBanner_MatchingTheRedirectFlag(
        bool created, bool saved, bool deleted, string expectedMessage)
    {
        var pageModel = CreatePageModel(new FakeDiseaseRankingFiltersApiService());

        await pageModel.OnGetAsync(created, saved, deleted, CancellationToken.None);

        Assert.Equal(expectedMessage, pageModel.SuccessMessage);
    }

    [Fact]
    public async Task OnGetAsync_DisplaysTheSelectedFilter_WhenFilterNumberIsProvided()
    {
        var pageModel = CreatePageModel(
            new FakeDiseaseRankingFiltersApiService(
                filters: [new DiseaseRankingFilterSummaryDto { FilterNumber = 1, DiseaseName = "Anthrax" }],
                filterDetail: ExistingFilter),
            new FakeSpeciesApiService(Species));
        pageModel.SelectedFilterNumber = 1;

        await pageModel.OnGetAsync(created: false, saved: false, deleted: false, CancellationToken.None);

        Assert.True(pageModel.ShowFilterPanel);
        Assert.False(pageModel.IsNewFilter);
        Assert.Equal("Anthrax", pageModel.Input.DiseaseName);
        Assert.Equal([SpeciesId], pageModel.SelectedSpeciesIds);
    }

    [Fact]
    public async Task OnPostCreateNewAsync_ReturnsThePage_WhenFiltersFailToLoad()
    {
        var pageModel = CreatePageModel(
            new FakeDiseaseRankingFiltersApiService(throwOnGetFilters: new HttpRequestException("down")));

        var result = await pageModel.OnPostCreateNewAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ShowFilterPanel);
    }

    [Fact]
    public async Task OnPostCreateNewAsync_OpensABlankPanel()
    {
        var pageModel = CreatePageModel(new FakeDiseaseRankingFiltersApiService(), new FakeSpeciesApiService(Species));

        var result = await pageModel.OnPostCreateNewAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.ShowFilterPanel);
        Assert.True(pageModel.IsNewFilter);
        Assert.Null(pageModel.SelectedFilterNumber);
    }

    [Fact]
    public async Task OnPostCreateNewAsync_SetsError_WhenSpeciesTreeFailsToLoad()
    {
        var pageModel = CreatePageModel(
            new FakeDiseaseRankingFiltersApiService(),
            new FakeSpeciesApiService(throwOnGetAllSpecies: new HttpRequestException("down")));

        await pageModel.OnPostCreateNewAsync(CancellationToken.None);

        Assert.True(pageModel.HasError);
        Assert.False(string.IsNullOrWhiteSpace(pageModel.ErrorMessage));
    }

    [Fact]
    public async Task OnPostSelectFilterAsync_ShowsSelectionError_WhenNoFilterIsSelected()
    {
        var pageModel = CreatePageModel(new FakeDiseaseRankingFiltersApiService());

        var result = await pageModel.OnPostSelectFilterAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ShowFilterPanel);
        Assert.False(string.IsNullOrWhiteSpace(pageModel.SelectionErrorMessage));
    }

    [Fact]
    public async Task OnPostSelectFilterAsync_ShowsSelectionError_WhenTheFilterCannotBeFound()
    {
        var pageModel = CreatePageModel(new FakeDiseaseRankingFiltersApiService());
        pageModel.SelectedFilterNumber = 99;

        await pageModel.OnPostSelectFilterAsync(CancellationToken.None);

        Assert.False(pageModel.ShowFilterPanel);
        Assert.False(string.IsNullOrWhiteSpace(pageModel.SelectionErrorMessage));
    }

    [Fact]
    public async Task OnPostSelectFilterAsync_OpensThePanel_WithTheFilterDetail()
    {
        var pageModel = CreatePageModel(
            new FakeDiseaseRankingFiltersApiService(filterDetail: ExistingFilter),
            new FakeSpeciesApiService(Species));
        pageModel.SelectedFilterNumber = 1;

        await pageModel.OnPostSelectFilterAsync(CancellationToken.None);

        Assert.True(pageModel.ShowFilterPanel);
        Assert.False(pageModel.IsNewFilter);
        Assert.Equal("Anthrax", pageModel.Input.DiseaseName);
    }

    [Fact]
    public async Task OnPostSaveAsync_AddsAModelError_WhenNoSpeciesAreSelected()
    {
        var pageModel = CreatePageModel(new FakeDiseaseRankingFiltersApiService(), new FakeSpeciesApiService(Species));
        pageModel.Input = new SaveDiseaseRankingFilterInput { DiseaseName = "Anthrax", Disease1 = "both", Disease2 = "both", Disease3 = "both", Disease4 = "both" };
        pageModel.SelectedSpeciesIds = [];

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
    }

    [Fact]
    public async Task OnPostSaveAsync_CreatesANewFilter_AndRedirectsWithCreatedFlag()
    {
        var api = new FakeDiseaseRankingFiltersApiService();
        var pageModel = CreatePageModel(api, new FakeSpeciesApiService(Species));
        pageModel.Input = new SaveDiseaseRankingFilterInput { DiseaseName = "Anthrax", Disease1 = "both", Disease2 = "both", Disease3 = "both", Disease4 = "both" };
        pageModel.SelectedSpeciesIds = [SpeciesId];
        pageModel.SelectedDiseaseTypes = ["bacteria"];

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(true, redirect.RouteValues?["created"]);
        Assert.NotNull(api.LastCreateRequest);
        Assert.Equal("Anthrax", api.LastCreateRequest!.DiseaseName);
    }

    [Fact]
    public async Task OnPostSaveAsync_UpdatesAnExistingFilter_AndRedirectsWithSavedFlag()
    {
        var api = new FakeDiseaseRankingFiltersApiService();
        var pageModel = CreatePageModel(api, new FakeSpeciesApiService(Species));
        pageModel.Input = new SaveDiseaseRankingFilterInput
        {
            FilterNumber = 1,
            DiseaseName = "Anthrax",
            Disease1 = "both",
            Disease2 = "both",
            Disease3 = "both",
            Disease4 = "both"
        };
        pageModel.SelectedSpeciesIds = [SpeciesId];

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(true, redirect.RouteValues?["saved"]);
        Assert.Equal(1L, api.LastUpdateRequest?.FilterNumber);
    }

    [Fact]
    public async Task OnPostSaveAsync_AddsAModelError_WhenTheApiReportsAFailure()
    {
        var api = new FakeDiseaseRankingFiltersApiService(
            saveResult: new SaveDiseaseRankingFilterResult { Outcome = DiseaseRankingFilterSaveOutcome.DuplicateName, ErrorMessage = "Filter name already exists" });
        var pageModel = CreatePageModel(api, new FakeSpeciesApiService(Species));
        pageModel.Input = new SaveDiseaseRankingFilterInput { DiseaseName = "Anthrax", Disease1 = "both", Disease2 = "both", Disease3 = "both", Disease4 = "both" };
        pageModel.SelectedSpeciesIds = [SpeciesId];

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.False(pageModel.ModelState.IsValid);
    }

    [Fact]
    public async Task OnPostDeleteAsync_DeletesTheFilter_AndRedirectsWithDeletedFlag()
    {
        var api = new FakeDiseaseRankingFiltersApiService();
        var pageModel = CreatePageModel(api);

        var result = await pageModel.OnPostDeleteAsync(1, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal(true, redirect.RouteValues?["deleted"]);
        Assert.Equal(1L, api.LastDeletedFilterNumber);
    }

    private static EditFiltersModel CreatePageModel(
        FakeDiseaseRankingFiltersApiService diseaseRankingFiltersApiService,
        FakeSpeciesApiService? speciesApiService = null) =>
        new(diseaseRankingFiltersApiService, speciesApiService ?? new FakeSpeciesApiService(Species), new AlwaysEnabledLogger<EditFiltersModel>());
}
