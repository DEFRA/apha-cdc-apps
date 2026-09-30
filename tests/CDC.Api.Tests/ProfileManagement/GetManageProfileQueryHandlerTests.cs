using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileManagement.Dtos;
using CDC.Api.Features.ProfileManagement.Interfaces;
using CDC.Api.Features.ProfileManagement.Queries;
using FluentAssertions;
using Moq;

namespace CDC.Api.Tests.ProfileManagement;

public class GetManageProfileQueryHandlerTests
{
    private readonly Mock<IProfileManagementService> service = new(MockBehavior.Strict);

    [Fact]
    public async Task Handle_ReturnsSuccess_WhenTheServiceReturnsAResponse()
    {
        var response = new GetManageProfileResponse
        {
            ProfileId = ProfileManagementTestData.ProfileId,
            ProfileTitle = "Bovine tuberculosis",
            ScenarioTitle = string.Empty,
            LatestPublishedVersionPublic = "Version 5.0",
            LatestPublishedVersionDefraNetOnly = "Version 7.0",
            LatestDraftVersion = "Version 8.0",
            ProfileStatus = "Draft"
        };

        service
            .Setup(svc => svc.GetManageProfileAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await new GetManageProfileQueryHandler(service.Object)
            .Handle(new GetManageProfileQuery(ProfileManagementTestData.ProfileId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(response);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenTheServiceReturnsNull()
    {
        service
            .Setup(svc => svc.GetManageProfileAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetManageProfileResponse?)null);

        var result = await new GetManageProfileQueryHandler(service.Object)
            .Handle(new GetManageProfileQuery(ProfileManagementTestData.ProfileId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public void Validator_RejectsAnEmptyProfileId()
    {
        var validator = new GetManageProfileQueryValidator();

        var result = validator.Validate(new GetManageProfileQuery(Guid.Empty));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Validator_AcceptsANonEmptyProfileId()
    {
        var validator = new GetManageProfileQueryValidator();

        var result = validator.Validate(new GetManageProfileQuery(Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }
}
