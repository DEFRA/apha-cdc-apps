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
    private static readonly Guid CidmSsoId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private readonly Mock<IUserService> service = new(MockBehavior.Strict);

    private static ResolveExternalUserCommand Command() => new()
    {
        CidmSsoId = CidmSsoId,
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
            CidmSsoId = CidmSsoId,
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
}
