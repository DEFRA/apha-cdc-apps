using CDC.Api.Application;
using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileManagement.Dtos;
using CDC.Api.Features.ProfileManagement.Interfaces;
using CDC.Api.Features.ProfileManagement.Queries;
using FluentAssertions;
using Moq;

namespace CDC.Api.Tests.ProfileManagement;

public class GetManageProfileQueryHandlerTests
{
    private static readonly Guid VersionId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    private readonly Mock<IProfileManagementService> service = new(MockBehavior.Strict);
    private readonly Mock<IProfileVersionGuard> versions = new(MockBehavior.Strict);

    private GetManageProfileQueryHandler CreateHandler() => new(service.Object, versions.Object);

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

        var result = await CreateHandler()
            .Handle(new GetManageProfileQuery(ProfileManagementTestData.ProfileId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(response);
        versions.Verify(guard => guard.EnsureCanBrowseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenTheServiceReturnsNull()
    {
        service
            .Setup(svc => svc.GetManageProfileAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GetManageProfileResponse?)null);

        var result = await CreateHandler()
            .Handle(new GetManageProfileQuery(ProfileManagementTestData.ProfileId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Handle_ChecksTheVersionTheCallerIsAboutToBrowse_BeforeReadingTheProfile()
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
        var calls = new List<string>();
        versions
            .Setup(guard => guard.EnsureCanBrowseAsync(ProfileManagementTestData.ProfileId, VersionId, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("guard"))
            .Returns(Task.CompletedTask);
        service
            .Setup(svc => svc.GetManageProfileAsync(ProfileManagementTestData.ProfileId, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("service"))
            .ReturnsAsync(response);

        var result = await CreateHandler()
            .Handle(new GetManageProfileQuery(ProfileManagementTestData.ProfileId, VersionId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        calls.Should().Equal("guard", "service");
    }

    [Fact]
    public async Task Handle_RefusesTheRequest_WithoutReadingTheProfile_WhenTheCallerMayNotBrowseTheVersion()
    {
        versions
            .Setup(guard => guard.EnsureCanBrowseAsync(ProfileManagementTestData.ProfileId, VersionId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        var act = () => CreateHandler()
            .Handle(new GetManageProfileQuery(ProfileManagementTestData.ProfileId, VersionId), CancellationToken.None);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        service.Verify(svc => svc.GetManageProfileAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
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

    [Fact]
    public void Validator_AcceptsAProfileVersionId()
    {
        var validator = new GetManageProfileQueryValidator();

        var result = validator.Validate(new GetManageProfileQuery(Guid.NewGuid(), VersionId));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validator_RejectsAnEmptyProfileVersionId()
    {
        var validator = new GetManageProfileQueryValidator();

        var result = validator.Validate(new GetManageProfileQuery(Guid.NewGuid(), Guid.Empty));

        result.IsValid.Should().BeFalse();
    }
}
