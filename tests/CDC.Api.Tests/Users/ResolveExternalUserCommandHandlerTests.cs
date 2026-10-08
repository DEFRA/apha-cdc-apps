using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.Users.Commands;
using CDC.Api.Features.Users.Interfaces;
using FluentAssertions;
using Moq;

namespace CDC.Api.Tests.Users;

public class ResolveExternalUserCommandHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SsoUserIdExt = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IUserService> service = new(MockBehavior.Strict);

    private static ResolveExternalUserCommand Command() => new()
    {
        SsoUserIdExt = SsoUserIdExt,
        Email = "user@example.com",
        FirstName = "Jane",
        LastName = "External",
        Organisation = "ACME Ltd"
    };

    [Fact]
    public async Task Handle_ReturnsMappedDto_OnSuccess()
    {
        var user = new ExternalUser
        {
            Id = UserId,
            UserName = "user@example.com",
            FullName = "Jane External",
            Organisation = "ACME Ltd",
            EmailAddress = "user@example.com",
            SsoUserIdExt = SsoUserIdExt,
            SsoUserId = null,
            IsProfileEditor = false,
            IsPolicyProfileUser = false
        };

        service
            .Setup(svc => svc.ResolveExternalUserAsync(Command(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(user));

        var result = await new ResolveExternalUserCommandHandler(service.Object).Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(UserId);
        result.Value.FullName.Should().Be("Jane External");
        result.Value.EmailAddress.Should().Be("user@example.com");
        result.Value.Organisation.Should().Be("ACME Ltd");
    }

    [Fact]
    public async Task Handle_ReturnsForbidden_WhenServiceDeniesTheSignIn()
    {
        service
            .Setup(svc => svc.ResolveExternalUserAsync(Command(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Forbidden<ExternalUser>("not permitted"));

        var result = await new ResolveExternalUserCommandHandler(service.Object).Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbidden);
        result.Error.Should().Be("not permitted");
    }

    [Fact]
    public async Task Handle_ReturnsNotFound_WhenServiceReturnsNotFound()
    {
        service
            .Setup(svc => svc.ResolveExternalUserAsync(Command(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.NotFound<ExternalUser>("not found"));

        var result = await new ResolveExternalUserCommandHandler(service.Object).Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
        result.Error.Should().Be("not found");
    }

    [Fact]
    public async Task Handle_ReturnsConflict_WhenServiceReturnsConflict()
    {
        service
            .Setup(svc => svc.ResolveExternalUserAsync(Command(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Conflict<ExternalUser>("conflict"));

        var result = await new ResolveExternalUserCommandHandler(service.Object).Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Conflict);
        result.Error.Should().Be("conflict");
    }

    [Fact]
    public async Task Handle_ReturnsForbidden_WhenServiceReturnsAnUnrecognisedStatus()
    {
        service
            .Setup(svc => svc.ResolveExternalUserAsync(Command(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Result<ExternalUser>((ResultStatus)(-1), null, "unexpected"));

        var result = await new ResolveExternalUserCommandHandler(service.Object).Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbidden);
        result.Error.Should().Be("unexpected");
    }
}
