using CDC.Api.Features.Species;
using CDC.Api.Features.Species.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.Species;

public class SpeciesServiceTests
{
    private readonly Mock<ISpeciesRepository> repository = new(MockBehavior.Strict);

    private SpeciesService CreateService() => new(repository.Object, NullLogger<SpeciesService>.Instance);

    [Fact]
    public async Task GetAllSpecies_ShouldReturnSpecies()
    {
        repository
            .Setup(repo => repo.GetAllSpeciesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([SpeciesTestData.Species()]);

        var result = await CreateService().GetAllSpeciesAsync(CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Id.Should().Be(SpeciesTestData.SpeciesId);
        result[0].Description.Should().Be("Cattle");
        result[0].IsActive.Should().BeTrue();
        repository.VerifyAll();
    }

    [Fact]
    public async Task GetSpeciesMetadata_ShouldReturnMetadata()
    {
        repository
            .Setup(repo => repo.GetSpeciesMetadataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpeciesTestData.Metadata());

        var result = await CreateService().GetSpeciesMetadataAsync(CancellationToken.None);

        result.Sections.Should().ContainSingle();
        result.Sections[0].Questions.Should().ContainSingle();
        result.Sections[0].Questions[0].Fields.Should().ContainSingle();
        result.Sections[0].Questions[0].Fields[0].DataTypeName.Should().Be("Boolean");
    }

    [Fact]
    public async Task GetSpeciesAnswerData_ShouldReturnAnswerData()
    {
        repository
            .Setup(repo => repo.GetSpeciesAnswerDataAsync(SpeciesTestData.SpeciesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpeciesTestData.AnswerData());

        var result = await CreateService().GetSpeciesAnswerDataAsync(SpeciesTestData.SpeciesId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.SpeciesName.Should().Be("Cattle");
        result.LastUpdated.Should().Equal(SpeciesTestData.RowVersion);
        result.Sections.Should().ContainSingle();
        result.Sections[0].FieldValues[0].BooleanValue.Should().BeTrue();
    }

    [Fact]
    public async Task GetSpeciesAnswerData_ShouldReturnNull_WhenSpeciesDoesNotExist()
    {
        repository
            .Setup(repo => repo.GetSpeciesAnswerDataAsync(SpeciesTestData.SpeciesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CDC.Api.Domain.Entities.SpeciesAnswerData?)null);

        var result = await CreateService().GetSpeciesAnswerDataAsync(SpeciesTestData.SpeciesId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllSelectedSpecies_ShouldReturnSelectedSpecies()
    {
        repository
            .Setup(repo => repo.GetAllSelectedSpeciesAsync("Bovine tuberculosis", It.IsAny<CancellationToken>()))
            .ReturnsAsync([SpeciesTestData.SelectedSpecies()]);

        var result = await CreateService().GetAllSelectedSpeciesAsync("Bovine tuberculosis", CancellationToken.None);

        result.Should().ContainSingle();
        result[0].DiseaseName.Should().Be("Bovine tuberculosis");
        result[0].Disease3.Should().Be(2);
        result[0].FilterNumber.Should().Be(7);
    }

    [Fact]
    public async Task UpdateSpeciesAnswerData_ShouldCallRepository()
    {
        var command = SpeciesTestData.UpdateCommand();

        repository
            .Setup(repo => repo.UpdateSpeciesAnswerDataAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpeciesTestData.NewRowVersion);

        var result = await CreateService().UpdateSpeciesAnswerDataAsync(command, CancellationToken.None);

        result.SpeciesId.Should().Be(SpeciesTestData.SpeciesId);
        result.LastUpdated.Should().Equal(SpeciesTestData.NewRowVersion);
        repository.Verify(
            repo => repo.UpdateSpeciesAnswerDataAsync(command, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateSpeciesAnswerData_ShouldRejectNullCommand()
    {
        var act = async () => await CreateService().UpdateSpeciesAnswerDataAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetSpeciesDetail_ShouldReturnDetail()
    {
        repository
            .Setup(repo => repo.GetSpeciesByIdAsync(SpeciesTestData.SpeciesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpeciesTestData.SpeciesDetail());

        var result = await CreateService().GetSpeciesDetailAsync(SpeciesTestData.SpeciesId, CancellationToken.None);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Dairy cattle");
        result.ParentName.Should().Be("Cattle");
    }

    [Fact]
    public async Task GetSpeciesDetail_ShouldReturnNull_WhenSpeciesDoesNotExist()
    {
        repository
            .Setup(repo => repo.GetSpeciesByIdAsync(SpeciesTestData.SpeciesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CDC.Api.Domain.Entities.SpeciesDetail?)null);

        var result = await CreateService().GetSpeciesDetailAsync(SpeciesTestData.SpeciesId, CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetSpeciesValidParents_ShouldReturnValidParents()
    {
        repository
            .Setup(repo => repo.GetSpeciesValidParentsAsync(SpeciesTestData.SpeciesId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([SpeciesTestData.ValidParent()]);

        var result = await CreateService().GetSpeciesValidParentsAsync(SpeciesTestData.SpeciesId, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("Cattle");
    }

    [Fact]
    public async Task UpdateSpeciesNameParent_ShouldCallRepository()
    {
        var command = SpeciesTestData.UpdateNameParentCommand();

        repository
            .Setup(repo => repo.UpdateSpeciesNameParentAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SpeciesTestData.NewRowVersion);

        var result = await CreateService().UpdateSpeciesNameParentAsync(command, CancellationToken.None);

        result.SpeciesId.Should().Be(SpeciesTestData.SpeciesId);
        result.LastUpdated.Should().Equal(SpeciesTestData.NewRowVersion);
    }

    [Fact]
    public async Task UpdateSpeciesNameParent_ShouldRejectNullCommand()
    {
        var act = async () => await CreateService().UpdateSpeciesNameParentAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task GetSpeciesAuditTrail_ShouldReturnEntries()
    {
        repository
            .Setup(repo => repo.GetSpeciesAuditTrailAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([SpeciesTestData.AuditTrailEntry()]);

        var result = await CreateService().GetSpeciesAuditTrailAsync(CancellationToken.None);

        result.Should().ContainSingle();
        result[0].ReasonForChange.Should().Be("Simplifying the name");
    }
}
