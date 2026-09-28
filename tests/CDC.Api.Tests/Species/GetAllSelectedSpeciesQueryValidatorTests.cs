using CDC.Api.Domain.Common;
using CDC.Api.Features.Species.Dtos;
using CDC.Api.Features.Species.Interfaces;
using CDC.Api.Features.Species.Queries;
using FluentAssertions;
using FluentValidation;
using Moq;

namespace CDC.Api.Tests.Species;

public class GetAllSelectedSpeciesQueryValidatorTests
{
    private readonly GetAllSelectedSpeciesQueryValidator validator = new();

    [Fact]
    public void Validate_WithValidDiseaseName_ShouldPass()
    {
        var query = new GetAllSelectedSpeciesQuery("Bovine tuberculosis");

        var result = validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptyDiseaseName_ShouldFail()
    {
        var query = new GetAllSelectedSpeciesQuery(string.Empty);

        var result = validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "A disease name is required.");
    }

    [Fact]
    public void Validate_WithTooLongDiseaseName_ShouldFail()
    {
        var longName = new string('x', 501);
        var query = new GetAllSelectedSpeciesQuery(longName);

        var result = validator.Validate(query);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(failure => failure.ErrorMessage == "A disease name must be 500 characters or fewer.");
    }

    [Fact]
    public void Validate_WithMaxLengthDiseaseName_ShouldPass()
    {
        var maxName = new string('x', 500);
        var query = new GetAllSelectedSpeciesQuery(maxName);

        var result = validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithWhitespaceOnlyDiseaseName_ShouldFail()
    {
        var query = new GetAllSelectedSpeciesQuery("   ");

        var result = validator.Validate(query);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validate_WithSpecialCharactersInDiseaseName_ShouldPass()
    {
        var query = new GetAllSelectedSpeciesQuery("Avian Influenza (H5N1)");

        var result = validator.Validate(query);

        result.IsValid.Should().BeTrue();
    }
}

public class GetAllSelectedSpeciesQueryHandlerTests
{
    [Fact]
    public async Task Handle_WithValidQuery_ReturnsSuccessResult()
    {
        var mockService = new Mock<ISpeciesService>();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var species = new List<SelectedSpeciesDto>
        {
            new() { Id = id1, Description = "Cattle", Disease1 = 1, FilterNumber = 0 },
            new() { Id = id2, Description = "Deer", Disease1 = 2, FilterNumber = 1 }
        };
        mockService
            .Setup(s => s.GetAllSelectedSpeciesAsync("Bovine tuberculosis", It.IsAny<CancellationToken>()))
            .ReturnsAsync(species);

        var handler = new GetAllSelectedSpeciesQueryHandler(mockService.Object);
        var query = new GetAllSelectedSpeciesQuery("Bovine tuberculosis");

        var result = await handler.Handle(query, CancellationToken.None);

        result.Should().NotBeNull();
        result.Status.Should().Be(ResultStatus.Success);
        result.Value.Should().HaveCount(2);
        result.Value![0].Description.Should().Be("Cattle");
    }

    [Fact]
    public async Task Handle_WithUnknownDiseaseName_ReturnsEmptyList()
    {
        var mockService = new Mock<ISpeciesService>();
        mockService
            .Setup(s => s.GetAllSelectedSpeciesAsync("Unknown Disease", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = new GetAllSelectedSpeciesQueryHandler(mockService.Object);
        var query = new GetAllSelectedSpeciesQuery("Unknown Disease");

        var result = await handler.Handle(query, CancellationToken.None);

        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        var mockService = new Mock<ISpeciesService>();
        var handler = new GetAllSelectedSpeciesQueryHandler(mockService.Object);

        var act = () => handler.Handle(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_PassesCancellationTokenToService()
    {
        var cts = new CancellationTokenSource();
        var mockService = new Mock<ISpeciesService>();
        mockService
            .Setup(s => s.GetAllSelectedSpeciesAsync("Test", cts.Token))
            .ReturnsAsync([]);

        var handler = new GetAllSelectedSpeciesQueryHandler(mockService.Object);
        var query = new GetAllSelectedSpeciesQuery("Test");

        await handler.Handle(query, cts.Token);

        mockService.Verify(s => s.GetAllSelectedSpeciesAsync("Test", cts.Token), Times.Once);
    }

    [Fact]
    public async Task Handle_PassesDiseaseNameToService()
    {
        const string diseaseName = "Avian Influenza";
        var mockService = new Mock<ISpeciesService>();
        mockService
            .Setup(s => s.GetAllSelectedSpeciesAsync(diseaseName, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = new GetAllSelectedSpeciesQueryHandler(mockService.Object);
        var query = new GetAllSelectedSpeciesQuery(diseaseName);

        await handler.Handle(query, CancellationToken.None);

        mockService.Verify(s => s.GetAllSelectedSpeciesAsync(diseaseName, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithMultipleSpecies_MaintainsOrder()
    {
        var mockService = new Mock<ISpeciesService>();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var id3 = Guid.NewGuid();
        var species = new List<SelectedSpeciesDto>
        {
            new() { Id = id1, Description = "Cattle", FilterNumber = 0 },
            new() { Id = id2, Description = "Pigs", FilterNumber = 1 },
            new() { Id = id3, Description = "Sheep", FilterNumber = 2 }
        };
        mockService
            .Setup(s => s.GetAllSelectedSpeciesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(species);

        var handler = new GetAllSelectedSpeciesQueryHandler(mockService.Object);
        var query = new GetAllSelectedSpeciesQuery("Test");

        var result = await handler.Handle(query, CancellationToken.None);

        result.Value.Should().HaveCount(3);
        result.Value![0].Description.Should().Be("Cattle");
        result.Value![1].Description.Should().Be("Pigs");
        result.Value![2].Description.Should().Be("Sheep");
    }
}
