using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileContributors.Commands;
using CDC.Api.Features.ProfileContributors.Dtos;
using CDC.Api.Features.ProfileContributors.Interfaces;
using CDC.Api.Features.ProfileContributors.Queries;
using CDC.Common.Contracts;
using FluentAssertions;
using MediatR;
using Moq;

namespace CDC.Api.Tests.ProfileContributors;

public class GetContributorForEditQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsSuccess_WhenTheContributorExists()
    {
        var profileId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();
        var dto = new ContributorEditDto { Id = contributorId, UserName = "carrie.batten" };
        var service = new Mock<IProfileContributorsService>(MockBehavior.Strict);
        service
            .Setup(svc => svc.GetContributorForEditAsync(profileId, contributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);
        var handler = new GetContributorForEditQueryHandler(service.Object);

        var result = await handler.Handle(new GetContributorForEditQuery(profileId, contributorId), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Success);
        result.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenTheContributorDoesNotExist()
    {
        var profileId = Guid.NewGuid();
        var contributorId = Guid.NewGuid();
        var service = new Mock<IProfileContributorsService>(MockBehavior.Strict);
        service
            .Setup(svc => svc.GetContributorForEditAsync(profileId, contributorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContributorEditDto?)null);
        var handler = new GetContributorForEditQueryHandler(service.Object);

        var result = await handler.Handle(new GetContributorForEditQuery(profileId, contributorId), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.NotFound);
    }
}

public class GetProfileUserRolesQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsEveryRole()
    {
        var roles = new List<ProfileUserRoleDto> { new() { Id = Guid.NewGuid(), Name = "Technical author", IsContributor = true } };
        var service = new Mock<IProfileContributorsService>(MockBehavior.Strict);
        service.Setup(svc => svc.GetProfileUserRolesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(roles);
        var handler = new GetProfileUserRolesQueryHandler(service.Object);

        var result = await handler.Handle(new GetProfileUserRolesQuery(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Success);
        result.Value.Should().BeSameAs(roles);
    }
}

public class UpdateContributorCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsTheServiceResult()
    {
        var command = new UpdateContributorCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Carrie Batten", "Pirbright Institute", [], [1, 2, 3, 4, 5, 6, 7, 8]);
        var service = new Mock<IProfileContributorsService>(MockBehavior.Strict);
        service
            .Setup(svc => svc.UpdateContributorAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));
        var handler = new UpdateContributorCommandHandler(service.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Success);
        service.VerifyAll();
    }
}

public class VerifyContributorUsernameQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsTheServiceResult()
    {
        var dto = new UserVerificationResultDto { Outcome = UserVerificationOutcome.ExistingUser, UserId = Guid.NewGuid() };
        var service = new Mock<IProfileContributorsService>(MockBehavior.Strict);
        service.Setup(svc => svc.VerifyUsernameAsync("internal\\carrie.batten", It.IsAny<CancellationToken>())).ReturnsAsync(dto);
        var handler = new VerifyContributorUsernameQueryHandler(service.Object);

        var result = await handler.Handle(new VerifyContributorUsernameQuery("internal\\carrie.batten"), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Success);
        result.Value.Should().BeSameAs(dto);
    }
}

public class AddContributorCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsTheServiceResult()
    {
        var command = new AddContributorCommand(
            Guid.NewGuid(), Guid.NewGuid(), "internal\\new.user", false, Guid.NewGuid(), "New User", "Pirbright Institute", []);
        var service = new Mock<IProfileContributorsService>(MockBehavior.Strict);
        service
            .Setup(svc => svc.AddContributorAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));
        var handler = new AddContributorCommandHandler(service.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Success);
        service.VerifyAll();
    }
}

public class DeleteContributorCommandHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsTheServiceResult()
    {
        var command = new DeleteContributorCommand(Guid.NewGuid(), Guid.NewGuid(), [1, 2, 3, 4, 5, 6, 7, 8]);
        var service = new Mock<IProfileContributorsService>(MockBehavior.Strict);
        service
            .Setup(svc => svc.DeleteContributorAsync(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Unit.Value));
        var handler = new DeleteContributorCommandHandler(service.Object);

        var result = await handler.Handle(command, CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Success);
        service.VerifyAll();
    }
}
