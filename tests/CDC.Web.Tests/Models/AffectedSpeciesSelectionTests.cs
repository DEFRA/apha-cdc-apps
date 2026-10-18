using CDC.Web.Models;

namespace CDC.Web.Tests.Models;

public class AffectedSpeciesSelectionTests
{
    private static readonly Guid Animals = Guid.NewGuid();
    private static readonly Guid Cattle = Guid.NewGuid();
    private static readonly Guid Bulls = Guid.NewGuid();
    private static readonly Guid Sheep = Guid.NewGuid();
    private static readonly Guid Goats = Guid.NewGuid();
    private static readonly Guid Draft = Guid.NewGuid();
    private static readonly Guid CloneSource = Guid.NewGuid();

    private static readonly IReadOnlyList<SpeciesDto> Species =
    [
        new() { Id = Animals, ParentId = Guid.Empty, Description = "Animals", IsActive = true },
        new() { Id = Cattle, ParentId = Animals, Description = "Cattle", IsActive = true },
        new() { Id = Bulls, ParentId = Cattle, Description = "Bulls", IsActive = true },
        new() { Id = Sheep, ParentId = Animals, Description = "Sheep", IsActive = true },
        new() { Id = Goats, ParentId = Animals, Description = "Goats", IsActive = false }
    ];

    private static AffectedSpeciesDto Affected(Guid id, string type) =>
        new() { SpeciesId = id, Name = "x", Type = type, IsActive = true };

    private static AffectedSpeciesSelectionResult Normalise(Guid[] profiled, Guid[] other, params Guid[] alreadySelected) =>
        AffectedSpeciesSelection.Normalise(Species, profiled, other, alreadySelected.ToHashSet());

    [Fact]
    public void Normalise_AcceptsAProfiledSpecies()
    {
        var result = Normalise([Sheep], []);

        Assert.Empty(result.Errors);
        Assert.Equal([Sheep], result.Profiled);
        Assert.Empty(result.Other);
    }

    [Fact]
    public void Normalise_RequiresAtLeastOneProfiledSpecies()
    {
        var result = Normalise([], [Sheep]);

        Assert.Equal([AffectedSpeciesSelection.NoProfiledSpeciesMessage], result.Errors);
    }

    [Fact]
    public void Normalise_KeepsOnlyTheTopMostSpeciesOfEachList()
    {
        var result = Normalise([Bulls, Cattle, Sheep], []);

        Assert.Equal([Cattle, Sheep], result.Profiled);
    }

    [Fact]
    public void Normalise_IgnoresDuplicates()
    {
        var result = Normalise([Sheep, Sheep], [Cattle, Cattle]);

        Assert.Equal([Sheep], result.Profiled);
        Assert.Equal([Cattle], result.Other);
    }

    [Fact]
    public void Normalise_RejectsASpeciesThatIsBothProfiledAndOther()
    {
        var result = Normalise([Sheep], [Sheep]);

        Assert.Contains("Sheep cannot be both a profiled species and another affected species", result.Errors);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Normalise_RejectsAGroupAndOneOfItsSpeciesPickedAsDifferentTypes(bool groupIsProfiled)
    {
        var result = groupIsProfiled ? Normalise([Cattle], [Bulls]) : Normalise([Bulls], [Cattle]);

        Assert.Single(result.Errors);
        Assert.Contains("belong to the same species group", result.Errors[0]);
    }

    [Fact]
    public void Normalise_RejectsAnUnknownSpecies()
    {
        var result = Normalise([Sheep, Guid.NewGuid()], []);

        Assert.Equal([AffectedSpeciesSelection.UnknownSpeciesMessage], result.Errors);
        Assert.Equal([Sheep], result.Profiled);
    }

    [Fact]
    public void Normalise_RejectsANewlyPickedInactiveSpecies()
    {
        var result = Normalise([Sheep], [Goats]);

        Assert.Equal(["Goats is inactive and cannot be selected"], result.Errors);
    }

    [Fact]
    public void Normalise_AllowsAnInactiveSpeciesThatWasAlreadySelected()
    {
        var result = Normalise([Sheep], [Goats], Goats);

        Assert.Empty(result.Errors);
        Assert.Equal([Goats], result.Other);
    }

    [Fact]
    public void Normalise_SurvivesAParentCycle()
    {
        IReadOnlyList<SpeciesDto> cyclic =
        [
            new() { Id = Cattle, ParentId = Sheep, Description = "Cattle", IsActive = true },
            new() { Id = Sheep, ParentId = Cattle, Description = "Sheep", IsActive = true }
        ];

        var result = AffectedSpeciesSelection.Normalise(cyclic, [Cattle, Sheep], [], new HashSet<Guid>());

        Assert.Empty(result.Errors);
    }

    [Fact]
    public void TopMost_ReducesAListToItsTopMostSpecies()
    {
        Assert.Equal([Cattle, Goats], AffectedSpeciesSelection.TopMost(Species, [Bulls, Cattle, Goats]));
    }

    [Fact]
    public void IdsOfType_TreatsAnythingButProfiledAsOther()
    {
        IReadOnlyList<AffectedSpeciesDto> affected = [Affected(Sheep, "Profiled"), Affected(Cattle, "Other"), Affected(Goats, "Anything")];

        Assert.Equal([Sheep], AffectedSpeciesSelection.IdsOfType(affected, AffectedSpeciesTypes.Profiled));
        Assert.Equal([Cattle, Goats], AffectedSpeciesSelection.IdsOfType(affected, AffectedSpeciesTypes.Other));
    }

    [Fact]
    public void PlanUpdate_AddsAndRemovesOnlyWhatChanged()
    {
        IReadOnlyList<AffectedSpeciesDto> stored = [Affected(Sheep, "Profiled"), Affected(Goats, "Other")];

        var (inserts, deletes) = AffectedSpeciesSelection.PlanUpdate(Draft, stored, [Sheep, Cattle], [Goats]);

        var insert = Assert.Single(inserts);
        Assert.Equal(Cattle, insert.SpeciesId);
        Assert.Equal("Profiled", insert.Type);
        Assert.Equal(Draft, insert.ProfileVersionId);
        Assert.Equal(Guid.Empty, insert.CloneProfileVersionId);
        Assert.Empty(deletes);
    }

    [Fact]
    public void PlanUpdate_RemovesAndReAddsASpeciesThatChangesType()
    {
        IReadOnlyList<AffectedSpeciesDto> stored = [Affected(Sheep, "Profiled"), Affected(Cattle, "Profiled")];

        var (inserts, deletes) = AffectedSpeciesSelection.PlanUpdate(Draft, stored, [Cattle], [Sheep]);

        var delete = Assert.Single(deletes);
        Assert.Equal(Sheep, delete.SpeciesId);
        Assert.Equal(Draft, delete.ProfileVersionId);
        var insert = Assert.Single(inserts);
        Assert.Equal(Sheep, insert.SpeciesId);
        Assert.Equal("Other", insert.Type);
    }

    [Fact]
    public void PlanUpdate_ChangesNothing_WhenTheSelectionIsUnchanged()
    {
        IReadOnlyList<AffectedSpeciesDto> stored = [Affected(Sheep, "Profiled")];

        var (inserts, deletes) = AffectedSpeciesSelection.PlanUpdate(Draft, stored, [Sheep], []);

        Assert.Empty(inserts);
        Assert.Empty(deletes);
    }

    [Fact]
    public void PlanCreate_AddsEverySpeciesToTheNewDraftWithTheCloneSource()
    {
        var inserts = AffectedSpeciesSelection.PlanCreate(Draft, CloneSource, [Sheep], [Cattle]);

        Assert.Collection(
            inserts,
            first =>
            {
                Assert.Equal((Draft, CloneSource, Sheep, "Profiled"), (first.ProfileVersionId, first.CloneProfileVersionId, first.SpeciesId, first.Type));
            },
            second =>
            {
                Assert.Equal((Draft, CloneSource, Cattle, "Other"), (second.ProfileVersionId, second.CloneProfileVersionId, second.SpeciesId, second.Type));
            });
    }
}
