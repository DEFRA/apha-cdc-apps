using CDC.Web.Models;

namespace CDC.Web.Tests.Models;

public class SpeciesTreeBuilderTests
{
    private static readonly Guid CattleId = Guid.Parse("6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f");
    private static readonly Guid DairyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid JerseyId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid InactiveChildId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid OrphanId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid MissingParentId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public void Build_NestsChildrenUnderTheirParent_ToUnlimitedDepth()
    {
        IReadOnlyList<SpeciesDto> species =
        [
            new SpeciesDto { Id = CattleId, ParentId = Guid.Empty, Description = "Cattle", IsActive = true },
            new SpeciesDto { Id = DairyId, ParentId = CattleId, Description = "Dairy cattle", IsActive = true },
            new SpeciesDto { Id = JerseyId, ParentId = DairyId, Description = "Jersey", IsActive = true }
        ];

        var tree = SpeciesTreeBuilder.Build(species);

        var root = Assert.Single(tree);
        Assert.Equal("Cattle", root.Label);
        var dairy = Assert.Single(root.Children);
        Assert.Equal("Dairy cattle", dairy.Label);
        var jersey = Assert.Single(dairy.Children);
        Assert.Equal("Jersey", jersey.Label);
    }

    [Fact]
    public void Build_ExcludesInactiveSpecies()
    {
        IReadOnlyList<SpeciesDto> species =
        [
            new SpeciesDto { Id = CattleId, ParentId = Guid.Empty, Description = "Cattle", IsActive = true },
            new SpeciesDto { Id = InactiveChildId, ParentId = CattleId, Description = "Retired breed", IsActive = false }
        ];

        var tree = SpeciesTreeBuilder.Build(species);

        var root = Assert.Single(tree);
        Assert.Empty(root.Children);
    }

    [Fact]
    public void Build_ExpandsOnlyRootNodes()
    {
        IReadOnlyList<SpeciesDto> species =
        [
            new SpeciesDto { Id = CattleId, ParentId = Guid.Empty, Description = "Cattle", IsActive = true },
            new SpeciesDto { Id = DairyId, ParentId = CattleId, Description = "Dairy cattle", IsActive = true }
        ];

        var tree = SpeciesTreeBuilder.Build(species);

        Assert.True(tree[0].Expanded);
        Assert.False(tree[0].Children[0].Expanded);
    }

    [Fact]
    public void Build_TreatsASpeciesWithAMissingActiveParent_AsARoot()
    {
        IReadOnlyList<SpeciesDto> species =
        [
            new SpeciesDto { Id = OrphanId, ParentId = MissingParentId, Description = "Orphan", IsActive = true }
        ];

        var tree = SpeciesTreeBuilder.Build(species);

        var root = Assert.Single(tree);
        Assert.Equal("Orphan", root.Label);
    }

    [Fact]
    public void Build_ReturnsEmpty_WhenNoSpeciesAreSupplied()
    {
        var tree = SpeciesTreeBuilder.Build([]);

        Assert.Empty(tree);
    }
}
