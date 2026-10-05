using CDC.Api.Features.ReferenceData.Dtos;
using CDC.Api.Features.ReferenceData.Interfaces;
using CDC.Api.Features.ReferenceData.Queries;
using FluentAssertions;
using Moq;

namespace CDC.Api.Tests.ReferenceData;

public sealed class GetReferenceValuesQueryHandlerTests
{
    private readonly Mock<IReferenceDataService> service = new(MockBehavior.Strict);

    [Fact]
    public async Task Handle_ReturnsSuccessWithServiceValues()
    {
        var referenceTableId = Guid.NewGuid();
        IReadOnlyList<ReferenceValueDto> values = [new ReferenceValueDto { Id = Guid.NewGuid(), Value = "Market records" }];

        service
            .Setup(svc => svc.GetReferenceValuesAsync(referenceTableId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(values);

        var result = await new GetReferenceValuesQueryHandler(service.Object)
            .Handle(new GetReferenceValuesQuery(referenceTableId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(values);
    }

    [Fact]
    public async Task Handle_Throws_WhenRequestIsNull()
    {
        var handler = new GetReferenceValuesQueryHandler(service.Object);

        var act = () => handler.Handle(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
