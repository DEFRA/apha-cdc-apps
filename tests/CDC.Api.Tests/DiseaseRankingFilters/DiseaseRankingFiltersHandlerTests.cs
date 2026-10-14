using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.DiseaseRankingFilters.Commands;
using CDC.Api.Features.DiseaseRankingFilters.Dtos;
using CDC.Api.Features.DiseaseRankingFilters.Interfaces;
using CDC.Api.Features.DiseaseRankingFilters.Queries;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.DiseaseRankingFilters;

public class DiseaseRankingFiltersHandlerTests
{
    private static readonly Guid SpeciesId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherSpeciesId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IDiseaseRankingFiltersRepository> repository = new(MockBehavior.Strict);

    [Fact]
    public async Task GetDiseaseRankingFiltersQueryHandler_ReturnsSuccess()
    {
        IReadOnlyList<DiseaseRankingFilterSummary> filters = [new DiseaseRankingFilterSummary { FilterNumber = 1, DiseaseName = "Anthrax" }];
        repository.Setup(repo => repo.GetFiltersAsync(It.IsAny<CancellationToken>())).ReturnsAsync(filters);

        var result = await new GetDiseaseRankingFiltersQueryHandler(repository.Object)
            .Handle(new GetDiseaseRankingFiltersQuery(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle();
        result.Value[0].FilterNumber.Should().Be(1);
        result.Value[0].DiseaseName.Should().Be("Anthrax");
    }

    [Fact]
    public async Task GetDiseaseRankingFilterDetailQueryHandler_ReturnsSuccess()
    {
        var detail = new DiseaseRankingFilterDetail
        {
            FilterNumber = 1,
            DiseaseName = "Anthrax",
            Disease1 = "endemic diseases",
            Disease2 = "both",
            Disease3 = "both",
            Disease4 = "both",
            DiseaseTypes = ["bacteria"],
            SpeciesIds = [SpeciesId]
        };
        repository.Setup(repo => repo.GetFilterDetailAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(detail);

        var result = await new GetDiseaseRankingFilterDetailQueryHandler(repository.Object)
            .Handle(new GetDiseaseRankingFilterDetailQuery(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FilterNumber.Should().Be(1);
        result.Value.DiseaseTypes.Should().Equal("bacteria");
        result.Value.SpeciesIds.Should().Equal(SpeciesId);
    }

    [Fact]
    public async Task GetDiseaseRankingFilterDetailQueryHandler_ReturnsNotFound_WhenNoActiveFilterExists()
    {
        repository.Setup(repo => repo.GetFilterDetailAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((DiseaseRankingFilterDetail?)null);

        var result = await new GetDiseaseRankingFilterDetailQueryHandler(repository.Object)
            .Handle(new GetDiseaseRankingFilterDetailQuery(99), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Error.Should().Contain("99");
    }

    [Fact]
    public async Task SaveDiseaseRankingFilterCommandHandler_CreatesANewFilter_WhenNoFilterNumberIsSupplied()
    {
        var request = new SaveDiseaseRankingFilterRequestDto { DiseaseName = "Anthrax", SpeciesIds = [SpeciesId, OtherSpeciesId] };

        repository.Setup(repo => repo.ReserveFilterNumberAsync("Anthrax", It.IsAny<CancellationToken>())).ReturnsAsync(7L);
        repository
            .Setup(repo => repo.InsertFilterSpeciesAsync(7, request, It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().Handle(new SaveDiseaseRankingFilterCommand(null, request), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FilterNumber.Should().Be(7);
        repository.Verify(repo => repo.InsertFilterSpeciesAsync(7, request, SpeciesId, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.InsertFilterSpeciesAsync(7, request, OtherSpeciesId, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.DeleteFilterAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SaveDiseaseRankingFilterCommandHandler_ReturnsConflict_WhenTheNameIsAlreadyInUse()
    {
        var request = new SaveDiseaseRankingFilterRequestDto { DiseaseName = "Anthrax", SpeciesIds = [SpeciesId] };
        repository.Setup(repo => repo.ReserveFilterNumberAsync("Anthrax", It.IsAny<CancellationToken>())).ReturnsAsync(0L);

        var result = await CreateHandler().Handle(new SaveDiseaseRankingFilterCommand(null, request), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Conflict);
        result.Error.Should().Contain("Anthrax");
    }

    [Fact]
    public async Task SaveDiseaseRankingFilterCommandHandler_ReplacesEverySpeciesRow_WhenAFilterNumberIsSupplied()
    {
        var request = new SaveDiseaseRankingFilterRequestDto { DiseaseName = "Anthrax", SpeciesIds = [SpeciesId] };

        repository.Setup(repo => repo.DeleteFilterAsync(1, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        repository
            .Setup(repo => repo.UpdateFilterSpeciesAsync(1, request, SpeciesId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateHandler().Handle(new SaveDiseaseRankingFilterCommand(1, request), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.FilterNumber.Should().Be(1);
        repository.Verify(repo => repo.DeleteFilterAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.UpdateFilterSpeciesAsync(1, request, SpeciesId, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.ReserveFilterNumberAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteDiseaseRankingFilterCommandHandler_ReturnsSuccess()
    {
        repository.Setup(repo => repo.DeleteFilterAsync(1, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var result = await new DeleteDiseaseRankingFilterCommandHandler(repository.Object, NullLogger<DeleteDiseaseRankingFilterCommandHandler>.Instance)
            .Handle(new DeleteDiseaseRankingFilterCommand(1), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Unit.Value);
        repository.Verify(repo => repo.DeleteFilterAsync(1, It.IsAny<CancellationToken>()), Times.Once);
    }

    private SaveDiseaseRankingFilterCommandHandler CreateHandler() =>
        new(repository.Object, NullLogger<SaveDiseaseRankingFilterCommandHandler>.Instance);
}
