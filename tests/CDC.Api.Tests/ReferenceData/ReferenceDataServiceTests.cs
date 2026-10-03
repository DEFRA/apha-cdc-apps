using CDC.Api.Domain.Entities;
using CDC.Api.Features.ReferenceData;
using CDC.Api.Features.ReferenceData.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.ReferenceData;

public sealed class ReferenceDataServiceTests
{
    private static readonly Guid ReferenceTableId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid ValueId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private readonly Mock<IReferenceDataRepository> repository = new(MockBehavior.Strict);

    private ReferenceDataService CreateService() =>
        new(repository.Object, NullLogger<ReferenceDataService>.Instance);

    [Fact]
    public async Task GetReferenceValuesAsync_MapsEachValue()
    {
        repository
            .Setup(repo => repo.GetReferenceValuesAsync(ReferenceTableId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([new ReferenceValue { Id = ValueId, Value = "Market records" }]);

        var result = await CreateService().GetReferenceValuesAsync(ReferenceTableId, CancellationToken.None);

        var value = result.Should().ContainSingle().Subject;
        value.Id.Should().Be(ValueId);
        value.Value.Should().Be("Market records");
    }

    [Fact]
    public async Task GetReferenceValuesAsync_ReturnsEmpty_WhenRepositoryHasNoValues()
    {
        repository
            .Setup(repo => repo.GetReferenceValuesAsync(ReferenceTableId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await CreateService().GetReferenceValuesAsync(ReferenceTableId, CancellationToken.None);

        result.Should().BeEmpty();
    }
}
