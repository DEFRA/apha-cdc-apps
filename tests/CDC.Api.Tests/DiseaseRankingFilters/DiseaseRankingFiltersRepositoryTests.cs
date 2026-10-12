using System.Data;
using CDC.Api.Features.DiseaseRankingFilters.Dtos;
using CDC.Api.Infrastructure;
using CDC.Api.Infrastructure.Repositories;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;

namespace CDC.Api.Tests.DiseaseRankingFilters;

public class DiseaseRankingFiltersRepositoryTests : IDisposable
{
    private static readonly Guid SpeciesId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherSpeciesId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly string[] FilterDetailColumns =
        ["FilterNumber", "DiseaseName", "Disease1", "Disease2", "Disease3", "Disease4", "Disease5", "SelectedSpecies"];

    // Dapper caches its compiled deserializer per (stored procedure, target type), not per actual
    // reader schema, so every scripted result set for this query must declare the same column
    // types - otherwise whichever test runs first silently decides the mapping for every other test.
    private static readonly Type[] FilterDetailColumnTypes =
        [typeof(long), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(Guid)];

    private readonly FakeDbConnection connection = new();
    private readonly Mock<ILogger<DiseaseRankingFiltersRepository>> logger = new();

    public DiseaseRankingFiltersRepositoryTests() =>
        logger.Setup(log => log.IsEnabled(It.IsAny<LogLevel>())).Returns(true);

    public void Dispose()
    {
        connection.Dispose();
        GC.SuppressFinalize(this);
    }

    private DiseaseRankingFiltersRepository CreateRepository() =>
        new(new StubConnectionFactory(connection), logger.Object);

    [Fact]
    public async Task GetFiltersAsync_ExecutesStoredProcedureAndMapsRows()
    {
        connection.Script(DiseaseRankingFiltersStoredProcedures.GetFilters, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["FilterNumber", "DiseaseName"], [[1L, "Anthrax"], [2L, "Bovine tuberculosis"]])]
        });

        var filters = await CreateRepository().GetFiltersAsync(CancellationToken.None);

        filters.Should().HaveCount(2);
        filters[0].FilterNumber.Should().Be(1);
        filters[0].DiseaseName.Should().Be("Anthrax");

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(DiseaseRankingFiltersStoredProcedures.GetFilters);
        executed.CommandType.Should().Be(CommandType.StoredProcedure);
    }

    [Fact]
    public async Task GetFilterDetailAsync_ReturnsNull_WhenNoActiveRowsExist()
    {
        connection.Script(DiseaseRankingFiltersStoredProcedures.GetFilterDetail, new FakeCommandScript
        {
            ResultSets = [FakeResultSet.Empty(FilterDetailColumns, FilterDetailColumnTypes)]
        });

        var detail = await CreateRepository().GetFilterDetailAsync(99, CancellationToken.None);

        detail.Should().BeNull();
    }

    [Fact]
    public async Task GetFilterDetailAsync_MapsCriteriaDiseaseTypesAndDistinctSpecies()
    {
        connection.Script(DiseaseRankingFiltersStoredProcedures.GetFilterDetail, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(
                    FilterDetailColumns,
                    [
                        [1L, "Anthrax", "endemic diseases", "both", "both", "both", "bacteria,virus", SpeciesId],
                        [1L, "Anthrax", "endemic diseases", "both", "both", "both", "bacteria,virus", SpeciesId],
                        [1L, "Anthrax", "endemic diseases", "both", "both", "both", "bacteria,virus", OtherSpeciesId]
                    ]) { ColumnTypes = FilterDetailColumnTypes }
            ]
        });

        var detail = await CreateRepository().GetFilterDetailAsync(1, CancellationToken.None);

        detail.Should().NotBeNull();
        detail!.DiseaseName.Should().Be("Anthrax");
        detail.DiseaseTypes.Should().Equal("bacteria", "virus");
        detail.SpeciesIds.Should().Equal(SpeciesId, OtherSpeciesId);

        connection.Executed.Should().ContainSingle()
            .Which.Parameters.Should().ContainKey("FilterNumber").WhoseValue.Should().Be(1L);
    }

    [Fact]
    public async Task GetFilterDetailAsync_TreatsAnEmptyDisease5AsNoDiseaseTypes()
    {
        connection.Script(DiseaseRankingFiltersStoredProcedures.GetFilterDetail, new FakeCommandScript
        {
            ResultSets =
            [
                new FakeResultSet(FilterDetailColumns, [[1L, "Anthrax", "endemic diseases", "both", "both", "both", null, SpeciesId]])
                    { ColumnTypes = FilterDetailColumnTypes }
            ]
        });

        var detail = await CreateRepository().GetFilterDetailAsync(1, CancellationToken.None);

        detail!.DiseaseTypes.Should().BeEmpty();
    }

    [Fact]
    public async Task ReserveFilterNumberAsync_PassesDiseaseNameAndReturnsTheReservedNumber()
    {
        connection.Script(DiseaseRankingFiltersStoredProcedures.ReserveFilterNumber, new FakeCommandScript
        {
            ResultSets = [new FakeResultSet(["Value"], [[7L]])]
        });

        var filterNumber = await CreateRepository().ReserveFilterNumberAsync("Anthrax", CancellationToken.None);

        filterNumber.Should().Be(7);
        connection.Executed.Should().ContainSingle()
            .Which.Parameters.Should().ContainKey("DiseaseName").WhoseValue.Should().Be("Anthrax");
    }

    [Fact]
    public async Task InsertFilterSpeciesAsync_SendsFilterNameCriteriaAndSpecies()
    {
        connection.Script(DiseaseRankingFiltersStoredProcedures.InsertFilterValue, new FakeCommandScript());
        var request = new SaveDiseaseRankingFilterRequestDto
        {
            DiseaseName = "Anthrax",
            Disease1 = "endemic diseases",
            Disease2 = "both",
            Disease3 = "both",
            Disease4 = "both",
            DiseaseTypes = ["bacteria", "virus"]
        };

        await CreateRepository().InsertFilterSpeciesAsync(7, request, SpeciesId, CancellationToken.None);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(DiseaseRankingFiltersStoredProcedures.InsertFilterValue);
        executed.Parameters["FilterNumber"].Should().Be(7L);
        executed.Parameters["DiseaseName"].Should().Be("Anthrax");
        executed.Parameters["Disease5"].Should().Be("bacteria,virus");
        executed.Parameters["SelectedSpecies"].Should().Be(SpeciesId);
    }

    [Fact]
    public async Task UpdateFilterSpeciesAsync_SendsFilterNameCriteriaAndSpecies()
    {
        connection.Script(DiseaseRankingFiltersStoredProcedures.UpdateFilterValue, new FakeCommandScript());
        var request = new SaveDiseaseRankingFilterRequestDto { DiseaseName = "Anthrax", DiseaseTypes = ["bacteria"] };

        await CreateRepository().UpdateFilterSpeciesAsync(7, request, SpeciesId, CancellationToken.None);

        var executed = connection.Executed.Should().ContainSingle().Subject;
        executed.CommandText.Should().Be(DiseaseRankingFiltersStoredProcedures.UpdateFilterValue);
        executed.Parameters["Disease5"].Should().Be("bacteria");
    }

    [Fact]
    public async Task DeleteFilterAsync_PassesTheFilterNumber()
    {
        connection.Script(DiseaseRankingFiltersStoredProcedures.DisableFilterValue, new FakeCommandScript());

        await CreateRepository().DeleteFilterAsync(1, CancellationToken.None);

        connection.Executed.Should().ContainSingle()
            .Which.Parameters.Should().ContainKey("FilterNumber").WhoseValue.Should().Be(1L);
    }

    [Fact]
    public async Task GetFiltersAsync_LogsAndRethrows_WhenTheStoredProcedureFails()
    {
        connection.Script(DiseaseRankingFiltersStoredProcedures.GetFilters, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetFiltersAsync(CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task GetFilterDetailAsync_LogsAndRethrows_WhenTheStoredProcedureFails()
    {
        connection.Script(DiseaseRankingFiltersStoredProcedures.GetFilterDetail, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().GetFilterDetailAsync(1, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task ReserveFilterNumberAsync_LogsAndRethrows_WhenTheStoredProcedureFails()
    {
        connection.Script(DiseaseRankingFiltersStoredProcedures.ReserveFilterNumber, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().ReserveFilterNumberAsync("Anthrax", CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task InsertFilterSpeciesAsync_LogsAndRethrows_WhenTheStoredProcedureFails()
    {
        connection.Script(DiseaseRankingFiltersStoredProcedures.InsertFilterValue, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().InsertFilterSpeciesAsync(7, new SaveDiseaseRankingFilterRequestDto(), SpeciesId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task UpdateFilterSpeciesAsync_LogsAndRethrows_WhenTheStoredProcedureFails()
    {
        connection.Script(DiseaseRankingFiltersStoredProcedures.UpdateFilterValue, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().UpdateFilterSpeciesAsync(7, new SaveDiseaseRankingFilterRequestDto(), SpeciesId, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    [Fact]
    public async Task DeleteFilterAsync_LogsAndRethrows_WhenTheStoredProcedureFails()
    {
        connection.Script(DiseaseRankingFiltersStoredProcedures.DisableFilterValue, new FakeCommandScript { Throws = new FakeDbException("boom") });

        var act = () => CreateRepository().DeleteFilterAsync(1, CancellationToken.None);

        await act.Should().ThrowAsync<FakeDbException>();
    }

    private sealed class StubConnectionFactory(FakeDbConnection connection) : IDbConnectionFactory
    {
        public IDbConnection CreateConnection() => connection;
    }
}
