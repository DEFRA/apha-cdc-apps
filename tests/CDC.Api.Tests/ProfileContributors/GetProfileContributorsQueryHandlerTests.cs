using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileContributors.Dtos;
using CDC.Api.Features.ProfileContributors.Interfaces;
using CDC.Api.Features.ProfileContributors.Queries;
using CDC.Common.Contracts;
using FluentAssertions;
using Moq;

namespace CDC.Api.Tests.ProfileContributors;

public class GetProfileContributorsQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsTheServiceResult()
    {
        var profileId = Guid.NewGuid();
        var pagedResult = new PagedResult<ContributorDto>
        {
            Items = [],
            PageNumber = 1,
            PageSize = 10,
            TotalRecords = 0
        };
        var service = new Mock<IProfileContributorsService>(MockBehavior.Strict);
        service
            .Setup(svc => svc.GetProfileContributorsAsync(profileId, 1, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pagedResult);
        var handler = new GetProfileContributorsQueryHandler(service.Object);

        var result = await handler.Handle(new GetProfileContributorsQuery(profileId, 1, 10), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Success);
        result.Value.Should().BeSameAs(pagedResult);
        service.VerifyAll();
    }
}
