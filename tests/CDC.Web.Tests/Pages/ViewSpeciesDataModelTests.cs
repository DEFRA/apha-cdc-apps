using CDC.Web.Models;
using CDC.Web.Pages;

namespace CDC.Web.Tests.Pages;

public class ViewSpeciesDataModelTests
{
    private static readonly Guid CattleId = Guid.Parse("6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f");
    private static readonly Guid DairyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RetiredId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Fact]
    public async Task OnGetAsync_BuildsTree_FromActiveSpeciesOnly()
    {
        IReadOnlyList<SpeciesDto> species =
        [
            new SpeciesDto { Id = CattleId, ParentId = Guid.Empty, Description = "Cattle", IsActive = true, IsInUse = true },
            new SpeciesDto { Id = DairyId, ParentId = CattleId, Description = "Dairy cattle", IsActive = true, IsInUse = true },
            new SpeciesDto { Id = RetiredId, ParentId = Guid.Empty, Description = "Retired species", IsActive = false, IsInUse = false }
        ];
        var pageModel = CreatePageModel(new FakeSpeciesApiService(species));

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.False(pageModel.HasError);
        var root = Assert.Single(pageModel.SpeciesTree.Nodes);
        Assert.Equal("Cattle", root.Label);
        Assert.True(root.Expanded);
        var child = Assert.Single(root.Children);
        Assert.Equal("Dairy cattle", child.Label);
    }

    [Fact]
    public async Task OnGetAsync_SetsError_WhenApiCallFails()
    {
        var pageModel = CreatePageModel(
            new FakeSpeciesApiService(throwOnGetAllSpecies: new HttpRequestException("connection refused")));

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.True(pageModel.HasError);
        Assert.False(string.IsNullOrWhiteSpace(pageModel.ErrorMessage));
        Assert.Empty(pageModel.SpeciesTree.Nodes);
    }

    [Fact]
    public async Task OnGetAsync_ProducesEmptyTree_WhenNoSpeciesAreActive()
    {
        IReadOnlyList<SpeciesDto> species =
        [
            new SpeciesDto { Id = RetiredId, ParentId = Guid.Empty, Description = "Retired species", IsActive = false, IsInUse = false }
        ];
        var pageModel = CreatePageModel(new FakeSpeciesApiService(species));

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.False(pageModel.HasError);
        Assert.Empty(pageModel.SpeciesTree.Nodes);
    }

    private static ViewSpeciesDataModel CreatePageModel(FakeSpeciesApiService speciesApiService) =>
        new(speciesApiService, new AlwaysEnabledLogger<ViewSpeciesDataModel>());
}
