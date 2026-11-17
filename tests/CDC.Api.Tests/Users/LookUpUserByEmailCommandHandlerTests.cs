using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.Users.Commands;
using CDC.Api.Features.Users.Interfaces;
using FluentAssertions;
using Moq;

namespace CDC.Api.Tests.Users;

public class LookUpUserByEmailCommandHandlerTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SsoUserIdInt = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private readonly Mock<IUserService> service = new(MockBehavior.Strict);

    private static LookUpUserByEmailCommand Command(UserLookupKind kind = UserLookupKind.Internal) => new()
    {
        EmailAddress = "user@example.com",
        Kind = kind
    };

    private static ExternalUser User() => new()
    {
        Id = UserId,
        UserName = "DEFRA\\jdoe",
        FullName = "Jo Internal",
        Organisation = "APHA",
        EmailAddress = "user@example.com",
        SsoUserIdExt = Guid.Empty,
        SsoUserId = null,
        IsProfileEditor = false,
        IsPolicyProfileUser = true
    };

    private LookUpUserByEmailCommandHandler CreateHandler() => new(service.Object);

    [Fact]
    public async Task Handle_ReturnsMappedDto_OnSuccess()
    {
        service
            .Setup(svc => svc.LookUpByEmailAsync(Command(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(User()));

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(UserId);
        result.Value.FullName.Should().Be("Jo Internal");
        result.Value.IsProfileEditor.Should().BeFalse();
        result.Value.IsPolicyProfileUser.Should().BeTrue();
        result.Value.IsInternalProfileEditor.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_FlagsAnInternalProfileEditor_WhenTheRowIsInternalAndAProfileEditor()
    {
        service
            .Setup(svc => svc.LookUpByEmailAsync(Command(UserLookupKind.External), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(User() with { SsoUserIdInt = SsoUserIdInt, IsProfileEditor = true }));

        var result = await CreateHandler().Handle(Command(UserLookupKind.External), CancellationToken.None);

        result.Value.IsProfileEditor.Should().BeTrue();
        result.Value.IsInternalProfileEditor.Should().BeTrue();
    }

    [Theory]
    [InlineData(ResultStatus.NotFound)]
    [InlineData(ResultStatus.Conflict)]
    [InlineData(ResultStatus.Forbidden)]
    public async Task Handle_PassesTheFailureOn_WhenTheServiceRefusesTheLookup(ResultStatus status)
    {
        Result<ExternalUser> refusal = status switch
        {
            ResultStatus.NotFound => Result.NotFound<ExternalUser>("none"),
            ResultStatus.Conflict => Result.Conflict<ExternalUser>("several"),
            _ => Result.Forbidden<ExternalUser>("not permitted")
        };
        service
            .Setup(svc => svc.LookUpByEmailAsync(Command(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(refusal);

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(status);
        result.Error.Should().Be(refusal.Error);
    }

    [Fact]
    public async Task Handle_TreatsAnyOtherFailureAsForbidden()
    {
        service
            .Setup(svc => svc.LookUpByEmailAsync(Command(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.ValidationFailed<ExternalUser>("invalid"));

        var result = await CreateHandler().Handle(Command(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
        result.Error.Should().Be("invalid");
    }
}
