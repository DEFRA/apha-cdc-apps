using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.Users.Commands;
using CDC.Api.Features.Users.Interfaces;
using FluentAssertions;
using Moq;

namespace CDC.Api.Tests.Users;

public class ResolveInternalUserCommandHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SsoUserIdInt = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IUserService> service = new(MockBehavior.Strict);

    private static ResolveInternalUserCommand Command() => new()
    {
        SsoUserIdInt = SsoUserIdInt,
        UserName = @"DEFRA\jdoe",
        FullName = "Jane Internal"
    };

    [Fact]
    public async Task Handle_ReturnsMappedDto_OnSuccess()
    {
        var user = new InternalUser
        {
            Id = UserId,
            UserName = @"DEFRA\jdoe",
            FullName = "Jane Internal",
            SsoUserIdInt = SsoUserIdInt,
            IsProfileEditor = false,
            IsPolicyProfileUser = false
        };

        service
            .Setup(svc => svc.ResolveInternalUserAsync(Command(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(user));

        var result = await new ResolveInternalUserCommandHandler(service.Object).Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(UserId);
        result.Value.FullName.Should().Be("Jane Internal");
    }

    [Fact]
    public async Task Handle_ReturnsLimitedAccessDto_WhenNoUserMatches()
    {
        var user = new InternalUser
        {
            Id = Guid.Empty,
            UserName = @"DEFRA\jdoe",
            FullName = "Jane Internal",
            SsoUserIdInt = null,
            IsProfileEditor = false,
            IsPolicyProfileUser = false
        };

        service
            .Setup(svc => svc.ResolveInternalUserAsync(Command(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(user));

        var result = await new ResolveInternalUserCommandHandler(service.Object).Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(Guid.Empty);
        result.Value.IsProfileEditor.Should().BeFalse();
        result.Value.IsPolicyProfileUser.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenNoUserMatches()
    {
        service
            .Setup(svc => svc.ResolveInternalUserAsync(Command(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<InternalUser>("not found"));

        var result = await new ResolveInternalUserCommandHandler(service.Object).Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Error.Should().Be("not found");
    }

    [Fact]
    public async Task Handle_ReturnsForbidden_WhenServiceReturnsForbidden()
    {
        service
            .Setup(svc => svc.ResolveInternalUserAsync(Command(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Forbidden<InternalUser>("not permitted"));

        var result = await new ResolveInternalUserCommandHandler(service.Object).Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbidden);
        result.Error.Should().Be("not permitted");
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenServiceReturnsAnUnrecognisedStatus()
    {
        service
            .Setup(svc => svc.ResolveInternalUserAsync(Command(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<InternalUser>((ResultStatus)(-1), null, "unexpected"));

        var result = await new ResolveInternalUserCommandHandler(service.Object).Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Error.Should().Be("unexpected");
    }
}
