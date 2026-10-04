using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.Users;
using CDC.Api.Features.Users.Commands;
using CDC.Api.Features.Users.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.Users;

public class UserServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CidmSsoId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SsoUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly Mock<IUserRepository> repository = new(MockBehavior.Strict);

    private UserService CreateService() => new(repository.Object, NullLogger<UserService>.Instance);

    private static ResolveExternalUserCommand Command() => new()
    {
        CidmSsoId = CidmSsoId,
        Email = "user@example.com",
        FirstName = "Jane",
        LastName = "External",
        Organisation = "ACME Ltd"
    };

    private static ExternalUser ExistingUser(Guid? ssoUserId) => new()
    {
        Id = UserId,
        UserName = "user@example.com",
        FullName = "Jane External",
        Organisation = "ACME Ltd",
        EmailAddress = "user@example.com",
        CidmSsoId = Guid.Empty,
        SsoUserId = ssoUserId,
        IsProfileEditor = false,
        IsPolicyProfileUser = false
    };

    [Fact]
    public async Task ResolveExternalUserAsync_ReturnsExistingUser_WhenMatchedByCidmSsoId()
    {
        var existing = ExistingUser(SsoUserId) with { CidmSsoId = CidmSsoId };

        repository
            .Setup(repo => repo.GetByCidmSsoIdAsync(CidmSsoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateService().ResolveExternalUserAsync(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(existing);

        repository.Verify(repo => repo.GetByEmailAddressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.UpdateCidmSsoIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.CreateExternalUserAsync(It.IsAny<ExternalUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveExternalUserAsync_LinksAndReturnsUser_WhenMatchedByEmailAndAlreadyExternal()
    {
        var existing = ExistingUser(SsoUserId);

        repository
            .Setup(repo => repo.GetByCidmSsoIdAsync(CidmSsoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalUser?)null);
        repository
            .Setup(repo => repo.GetByEmailAddressAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repository
            .Setup(repo => repo.UpdateCidmSsoIdAsync(UserId, CidmSsoId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateService().ResolveExternalUserAsync(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(UserId);
        result.Value.CidmSsoId.Should().Be(CidmSsoId);

        repository.Verify(repo => repo.UpdateCidmSsoIdAsync(UserId, CidmSsoId, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.CreateExternalUserAsync(It.IsAny<ExternalUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveExternalUserAsync_ReturnsForbidden_WhenMatchedByEmailButNotExternal()
    {
        var existing = ExistingUser(ssoUserId: null);

        repository
            .Setup(repo => repo.GetByCidmSsoIdAsync(CidmSsoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalUser?)null);
        repository
            .Setup(repo => repo.GetByEmailAddressAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateService().ResolveExternalUserAsync(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbidden);
        result.Error.Should().Contain("user@example.com");

        repository.Verify(repo => repo.UpdateCidmSsoIdAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.CreateExternalUserAsync(It.IsAny<ExternalUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveExternalUserAsync_CreatesNewUser_WhenNoExistingRowMatches()
    {
        repository
            .Setup(repo => repo.GetByCidmSsoIdAsync(CidmSsoId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalUser?)null);
        repository
            .Setup(repo => repo.GetByEmailAddressAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalUser?)null);

        ExternalUser? created = null;
        repository
            .Setup(repo => repo.CreateExternalUserAsync(It.IsAny<ExternalUser>(), It.IsAny<CancellationToken>()))
            .Callback<ExternalUser, CancellationToken>((user, _) => created = user)
            .ReturnsAsync((ExternalUser user, CancellationToken _) => user);

        var result = await CreateService().ResolveExternalUserAsync(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        created.Should().NotBeNull();
        created!.UserName.Should().Be("user@example.com");
        created.EmailAddress.Should().Be("user@example.com");
        created.FullName.Should().Be("Jane External");
        created.Organisation.Should().Be("ACME Ltd");
        created.CidmSsoId.Should().Be(CidmSsoId);
        created.SsoUserId.Should().BeNull();
        created.Id.Should().NotBe(Guid.Empty);

        result.Value.Should().BeSameAs(created);
    }

    [Fact]
    public async Task ResolveExternalUserAsync_Throws_WhenCommandIsNull()
    {
        var act = async () => await CreateService().ResolveExternalUserAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
