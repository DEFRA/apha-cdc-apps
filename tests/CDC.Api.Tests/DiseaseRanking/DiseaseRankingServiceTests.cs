using CDC.Api.Domain.Entities;
using CDC.Api.Features.DiseaseRanking;
using CDC.Api.Features.DiseaseRanking.Interfaces;
using CDC.Api.Features.Species.Interfaces;
using CDC.Common.Contracts;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SpeciesEntity = CDC.Api.Domain.Entities.Species;

namespace CDC.Api.Tests.DiseaseRanking;

public sealed class DiseaseRankingServiceTests
{
    private static readonly Guid FilterId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid MammalsId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid CattleId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid FishId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid TroutId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");
    private static readonly Guid RainbowTroutId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000003");
    private static readonly Guid MolluscsId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    private static readonly Guid OystersId = Guid.Parse("cccccccc-0000-0000-0000-000000000002");
    private static readonly Guid OrphanId = Guid.Parse("dddddddd-0000-0000-0000-000000000001");
    private static readonly Guid CycleAId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
    private static readonly Guid CycleBId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000002");

    private readonly Mock<IDiseaseRankingRepository> repository = new(MockBehavior.Strict);
    private readonly Mock<ISpeciesRepository> speciesRepository = new(MockBehavior.Strict);

    private DiseaseRankingService CreateService() =>
        new(repository.Object, speciesRepository.Object, NullLogger<DiseaseRankingService>.Instance);

    private static SpeciesEntity Species(Guid id, Guid parentId, string name) => new()
    {
        Id = id,
        ParentId = parentId,
        Description = name,
        IsActive = true,
        IsInUse = true
    };

    private static RankedProfile Ranked(Guid speciesId, string title, int publicHealth = 5) => new()
    {
        ProfileId = Guid.NewGuid(),
        SpeciesId = speciesId,
        ProfileTitle = title,
        SpeciesName = "Species",
        IsExotic = false,
        IsAwaitingValidation = false,
        PublicHealthRank = publicHealth
    };

    private void SetUpSpecies() =>
        speciesRepository
            .Setup(repo => repo.GetAllSpeciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Species(MammalsId, Guid.Empty, "Mammals"),
                Species(CattleId, MammalsId, "Cattle"),
                Species(FishId, Guid.Empty, "Fish"),
                Species(TroutId, FishId, "Trout"),
                Species(RainbowTroutId, TroutId, "Rainbow trout"),
                Species(MolluscsId, Guid.Empty, "Molluscs"),
                Species(OystersId, MolluscsId, "Oysters"),
                Species(CycleAId, CycleBId, "Cycle A"),
                Species(CycleBId, CycleAId, "Cycle B")
            ]);

    private void SetUpRankedProfiles(string? filterName, params RankedProfile[] profiles) =>
        repository
            .Setup(repo => repo.GetRankedProfilesAsync(filterName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(profiles);

    [Fact]
    public async Task GetReportOptionsAsync_ListsTerrestrialAndAquaticThenEachFilter()
    {
        repository
            .Setup(repo => repo.GetFiltersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DiseaseRankingFilter(FilterId, "  <p>ALL VIRAL&nbsp;DISEASES</p> ")]);

        var options = await CreateService().GetReportOptionsAsync(CancellationToken.None);

        options.Select(option => option.Key).Should().Equal("terrestrial", "aquatic", FilterId.ToString());
        options[0].Label.Should().Be("Disease ranking report: Terrestrial diseases");
        options[1].Label.Should().Be("Disease ranking report: Aquatic diseases");
        options[2].Label.Should().Be("All viral diseases");
    }

    [Fact]
    public async Task GetReportOptionsAsync_KeepsABlankFilterNameBlank()
    {
        repository
            .Setup(repo => repo.GetFiltersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DiseaseRankingFilter(FilterId, "   ")]);

        var options = await CreateService().GetReportOptionsAsync(CancellationToken.None);

        options[2].Label.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReportAsync_Terrestrial_KeepsOnlyNonAquaticSpecies()
    {
        SetUpSpecies();
        SetUpRankedProfiles(
            null,
            Ranked(CattleId, "Anthrax"),
            Ranked(TroutId, "Furunculosis"),
            Ranked(OystersId, "Bonamia"),
            Ranked(OrphanId, "Unknown species"));

        var report = await CreateService().GetReportAsync("Terrestrial", DiseaseRankingReportKind.Scored, CancellationToken.None);

        report.Should().NotBeNull();
        report!.Title.Should().EndWith("Terrestrial Diseases");
        report.FooterTitle.Should().Be("RFI scoring report for Terrestrial.");
        report.TopTables[0].Rows.Select(row => row.Disease).Should().BeEquivalentTo(["Anthrax (Species)", "Unknown species (Species)"]);
    }

    [Fact]
    public async Task GetReportAsync_Aquatic_KeepsFishAndMolluscsAtAnyDepth()
    {
        SetUpSpecies();
        SetUpRankedProfiles(
            null,
            Ranked(CattleId, "Anthrax"),
            Ranked(RainbowTroutId, "Furunculosis"),
            Ranked(OystersId, "Bonamia"));

        var report = await CreateService().GetReportAsync("aquatic", DiseaseRankingReportKind.Ranked, CancellationToken.None);

        report!.Title.Should().EndWith("Aquatic Diseases");
        report.FooterTitle.Should().Be("Disease ranking report for Aquatic.");
        report.TopTables[0].Rows.Select(row => row.Disease).Should().BeEquivalentTo(["Furunculosis (Species)", "Bonamia (Species)"]);
    }

    [Fact]
    public async Task GetReportAsync_Aquatic_SurvivesACyclicSpeciesHierarchy()
    {
        SetUpSpecies();
        SetUpRankedProfiles(null, Ranked(CycleAId, "Looping"));

        var report = await CreateService().GetReportAsync("aquatic", DiseaseRankingReportKind.Scored, CancellationToken.None);

        report!.TopTables[0].Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task GetReportAsync_WithAFilterId_ReadsTheNamedFilterAndUsesItsNameInTheHeading()
    {
        repository
            .Setup(repo => repo.GetFiltersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new DiseaseRankingFilter(Guid.NewGuid(), "Other"), new DiseaseRankingFilter(FilterId, "All viral diseases")]);
        SetUpRankedProfiles("All viral diseases", Ranked(CattleId, "Anthrax"));

        var report = await CreateService().GetReportAsync(FilterId.ToString(), DiseaseRankingReportKind.Scored, CancellationToken.None);

        report!.Title.Should().EndWith("for All viral diseases");
        report.FooterTitle.Should().Be("RFI scoring report for All viral diseases.");
        report.TopTables[0].Rows.Should().ContainSingle();
    }

    [Fact]
    public async Task GetReportAsync_ReturnsNull_WhenTheFilterIdIsNotKnown()
    {
        repository
            .Setup(repo => repo.GetFiltersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var report = await CreateService().GetReportAsync(FilterId.ToString(), DiseaseRankingReportKind.Scored, CancellationToken.None);

        report.Should().BeNull();
    }

    [Fact]
    public async Task GetReportAsync_WithWhitespaceAroundTheKey_UsesTheReport()
    {
        SetUpSpecies();
        SetUpRankedProfiles(null, Ranked(CattleId, "Anthrax"));

        var report = await CreateService().GetReportAsync("  terrestrial  ", DiseaseRankingReportKind.Scored, CancellationToken.None);

        report.Should().NotBeNull();
        report!.FooterTitle.Should().Be("RFI scoring report for Terrestrial.");
    }

    [Fact]
    public async Task GetReportAsync_ReturnsNull_WhenTheKeyIsNeitherAGroupNorAGuid()
    {
        var report = await CreateService().GetReportAsync("not-a-report", DiseaseRankingReportKind.Scored, CancellationToken.None);

        report.Should().BeNull();
    }
}
