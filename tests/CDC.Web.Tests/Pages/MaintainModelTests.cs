using CDC.Web.Models;
using CDC.Web.Pages.SpeciesData;

namespace CDC.Web.Tests.Pages;

public class MaintainModelTests
{
    private static readonly Guid CattleId = Guid.Parse("6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f");
    private static readonly Guid DairyId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly byte[] RowVersion = [0, 0, 0, 0, 0, 0, 0, 1];

    private static readonly IReadOnlyList<SpeciesDto> Species =
    [
        new SpeciesDto { Id = CattleId, ParentId = Guid.Empty, Description = "Cattle", IsActive = true, IsInUse = true },
        new SpeciesDto { Id = DairyId, ParentId = CattleId, Description = "Dairy cattle", IsActive = true, IsInUse = true }
    ];

    private static readonly SpeciesDetailDto DairyDetail = new()
    {
        Id = DairyId,
        Name = "Dairy cattle",
        ParentId = CattleId,
        ParentName = "Cattle",
        IsActive = true,
        IsInUse = true,
        LastUpdated = RowVersion
    };

    [Fact]
    public async Task OnGetAsync_BuildsTree_AndShowsSuccessBanner_WhenSaved()
    {
        var pageModel = CreatePageModel(new FakeSpeciesApiService(Species));

        await pageModel.OnGetAsync(saved: true, CancellationToken.None);

        Assert.False(pageModel.HasError);
        Assert.NotEmpty(pageModel.SpeciesTree.Nodes);
        Assert.False(string.IsNullOrWhiteSpace(pageModel.SuccessMessage));
    }

    [Fact]
    public async Task OnGetAsync_SetsError_WhenApiCallFails()
    {
        var pageModel = CreatePageModel(new FakeSpeciesApiService(throwOnGetAllSpecies: new HttpRequestException("down")));

        await pageModel.OnGetAsync(saved: false, CancellationToken.None);

        Assert.True(pageModel.HasError);
        Assert.False(string.IsNullOrWhiteSpace(pageModel.ErrorMessage));
    }

    [Fact]
    public async Task OnPostEditNameParentAsync_ShowsSelectionError_WhenNoSpeciesSelected()
    {
        var pageModel = CreatePageModel(new FakeSpeciesApiService(Species));

        await pageModel.OnPostEditNameParentAsync(CancellationToken.None);

        Assert.False(pageModel.ShowEditPanel);
        Assert.False(string.IsNullOrWhiteSpace(pageModel.SelectionErrorMessage));
    }

    [Fact]
    public async Task OnPostEditNameParentAsync_ShowsSelectionError_WhenSpeciesNotFound()
    {
        var pageModel = CreatePageModel(new FakeSpeciesApiService(Species));
        pageModel.SelectedSpeciesId = DairyId;

        await pageModel.OnPostEditNameParentAsync(CancellationToken.None);

        Assert.False(pageModel.ShowEditPanel);
        Assert.False(string.IsNullOrWhiteSpace(pageModel.SelectionErrorMessage));
    }

    [Fact]
    public async Task OnPostEditNameParentAsync_OpensPanel_WithOldNameAndOldParent()
    {
        var pageModel = CreatePageModel(new FakeSpeciesApiService(Species, speciesDetail: DairyDetail));
        pageModel.SelectedSpeciesId = DairyId;

        await pageModel.OnPostEditNameParentAsync(CancellationToken.None);

        Assert.True(pageModel.ShowEditPanel);
        Assert.Equal("Dairy cattle", pageModel.SpeciesDetail?.Name);
        Assert.Equal("Cattle", pageModel.SpeciesDetail?.ParentName);
        Assert.Equal(DairyId, pageModel.Input.SpeciesId);
        Assert.Equal("Dairy cattle", pageModel.Input.Name);
        Assert.Equal(Convert.ToBase64String(RowVersion), pageModel.Input.LastUpdatedBase64);
    }

    [Fact]
    public async Task OnPostSaveAsync_FailsValidation_WhenNameAndReasonAreMissing()
    {
        var pageModel = CreatePageModel(new FakeSpeciesApiService(Species, speciesDetail: DairyDetail));
        pageModel.Input = new EditNameParentInput
        {
            SpeciesId = DairyId,
            Name = string.Empty,
            Reason = string.Empty,
            LastUpdatedBase64 = Convert.ToBase64String(RowVersion)
        };

        await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.True(pageModel.ShowEditPanel);
        Assert.False(pageModel.ModelState.IsValid);
        Assert.True(pageModel.ModelState.ContainsKey("Input.Name"));
        Assert.True(pageModel.ModelState.ContainsKey("Input.Reason"));
    }

    [Fact]
    public async Task OnPostSaveAsync_Redirects_WhenValid()
    {
        var pageModel = CreatePageModel(new FakeSpeciesApiService(
            Species,
            speciesDetail: DairyDetail,
            updateResult: new UpdateSpeciesNameParentResult { Outcome = SpeciesUpdateOutcome.Success }));
        pageModel.Input = new EditNameParentInput
        {
            SpeciesId = DairyId,
            Name = "Dairy",
            ParentId = CattleId,
            Reason = "Simplifying the name",
            LastUpdatedBase64 = Convert.ToBase64String(RowVersion)
        };

        var result = await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToPageResult>(result);
        Assert.True(pageModel.ModelState.IsValid);
    }

    [Fact]
    public async Task OnPostSaveAsync_ShowsConflict_WhenApiReportsConflict()
    {
        var pageModel = CreatePageModel(new FakeSpeciesApiService(
            Species,
            speciesDetail: DairyDetail,
            updateResult: new UpdateSpeciesNameParentResult
            {
                Outcome = SpeciesUpdateOutcome.Conflict,
                ErrorMessage = "Another user has changed this species."
            }));
        pageModel.Input = new EditNameParentInput
        {
            SpeciesId = DairyId,
            Name = "Dairy",
            Reason = "Simplifying the name",
            LastUpdatedBase64 = Convert.ToBase64String(RowVersion)
        };

        await pageModel.OnPostSaveAsync(CancellationToken.None);

        Assert.True(pageModel.ShowEditPanel);
        Assert.False(pageModel.ModelState.IsValid);
    }

    [Fact]
    public async Task OnPostCancelAsync_HidesPanel_WithoutCallingUpdate()
    {
        var pageModel = CreatePageModel(new FakeSpeciesApiService(Species));

        await pageModel.OnPostCancelAsync(CancellationToken.None);

        Assert.False(pageModel.ShowEditPanel);
    }

    [Fact]
    public async Task OnGetAuditTrailAsync_LoadsEntries()
    {
        IReadOnlyList<SpeciesAuditTrailEntryDto> auditTrail =
        [
            new SpeciesAuditTrailEntryDto
            {
                Id = Guid.NewGuid(),
                OldName = "Dairy cattle",
                NewName = "Dairy",
                OldParent = "Cattle",
                NewParent = "Cattle",
                ChangedBy = "a.user",
                LogDate = DateTime.UtcNow,
                ReasonForChange = "Simplifying the name"
            }
        ];
        var pageModel = CreatePageModel(new FakeSpeciesApiService(Species, auditTrail: auditTrail));

        await pageModel.OnGetAuditTrailAsync(CancellationToken.None);

        Assert.True(pageModel.ShowAuditTrail);
        Assert.Single(pageModel.AuditTrail);
    }

    private static MaintainModel CreatePageModel(FakeSpeciesApiService speciesApiService) =>
        new(speciesApiService, new AlwaysEnabledLogger<MaintainModel>());
}
