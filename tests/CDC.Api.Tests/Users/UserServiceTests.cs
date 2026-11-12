using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.Users;
using CDC.Api.Features.Users.Commands;
using CDC.Api.Features.Users.Interfaces;
using CDC.Api.Tests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CDC.Api.Tests.Users;

public class UserServiceTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid SsoUserIdExt = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid SsoUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private readonly Mock<IUserRepository> repository = new(MockBehavior.Strict);

    private UserService CreateService() => new(repository.Object, NullLogger<UserService>.Instance);

    private static ResolveExternalUserCommand Command() => new()
    {
        SsoUserIdExt = SsoUserIdExt,
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
        SsoUserIdExt = Guid.Empty,
        SsoUserId = ssoUserId,
        IsProfileEditor = false,
        IsPolicyProfileUser = false
    };

    [Fact]
    public async Task ResolveExternalUserAsync_ReturnsExistingUser_WhenMatchedBySsoUserIdExt()
    {
        var existing = ExistingUser(SsoUserId) with { SsoUserIdExt = SsoUserIdExt };

        repository
            .Setup(repo => repo.GetBySsoUserIdExtAsync(SsoUserIdExt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateService().ResolveExternalUserAsync(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(existing);

        repository.Verify(repo => repo.GetByEmailAddressAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.UpdateSsoUserIdExtAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.CreateExternalUserAsync(It.IsAny<ExternalUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveExternalUserAsync_LinksAndReturnsUser_WhenMatchedByEmailAndAlreadyExternal()
    {
        var existing = ExistingUser(SsoUserId);

        repository
            .Setup(repo => repo.GetBySsoUserIdExtAsync(SsoUserIdExt, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalUser?)null);
        repository
            .Setup(repo => repo.GetByEmailAddressAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repository
            .Setup(repo => repo.UpdateSsoUserIdExtAsync(UserId, SsoUserIdExt, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateService().ResolveExternalUserAsync(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(UserId);
        result.Value.SsoUserIdExt.Should().Be(SsoUserIdExt);

        repository.Verify(repo => repo.UpdateSsoUserIdExtAsync(UserId, SsoUserIdExt, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.CreateExternalUserAsync(It.IsAny<ExternalUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveExternalUserAsync_ReturnsForbidden_WhenMatchedByEmailButNotExternal()
    {
        var existing = ExistingUser(ssoUserId: null);

        repository
            .Setup(repo => repo.GetBySsoUserIdExtAsync(SsoUserIdExt, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalUser?)null);
        repository
            .Setup(repo => repo.GetByEmailAddressAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateService().ResolveExternalUserAsync(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbidden);
        result.Error.Should().Contain("user@example.com");

        repository.Verify(repo => repo.UpdateSsoUserIdExtAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.CreateExternalUserAsync(It.IsAny<ExternalUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveExternalUserAsync_AllowsWithoutLinking_WhenMatchedByEmailAndInternalProfileEditor()
    {
        var existing = ExistingUser(ssoUserId: null) with { SsoUserIdInt = SsoUserIdInt, IsProfileEditor = true };

        repository
            .Setup(repo => repo.GetBySsoUserIdExtAsync(SsoUserIdExt, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalUser?)null);
        repository
            .Setup(repo => repo.GetByEmailAddressAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateService().ResolveExternalUserAsync(Command(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(existing);

        repository.Verify(repo => repo.UpdateSsoUserIdExtAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveExternalUserAsync_ReturnsForbidden_WhenMatchedByEmailAndInternalButNotProfileEditor()
    {
        var existing = ExistingUser(ssoUserId: null) with { SsoUserIdInt = SsoUserIdInt, IsProfileEditor = false };

        repository
            .Setup(repo => repo.GetBySsoUserIdExtAsync(SsoUserIdExt, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalUser?)null);
        repository
            .Setup(repo => repo.GetByEmailAddressAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateService().ResolveExternalUserAsync(Command(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
        repository.Verify(repo => repo.UpdateSsoUserIdExtAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveExternalUserAsync_ReturnsForbidden_WhenNoExistingRowMatches()
    {
        repository
            .Setup(repo => repo.GetBySsoUserIdExtAsync(SsoUserIdExt, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalUser?)null);
        repository
            .Setup(repo => repo.GetByEmailAddressAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((ExternalUser?)null);

        var result = await CreateService().ResolveExternalUserAsync(Command(), CancellationToken.None);

        result.Status.Should().Be(ResultStatus.Forbidden);
        repository.Verify(repo => repo.CreateExternalUserAsync(It.IsAny<ExternalUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveExternalUserAsync_Throws_WhenCommandIsNull()
    {
        var act = async () => await CreateService().ResolveExternalUserAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static readonly Guid SsoUserIdInt = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static ResolveInternalUserCommand InternalCommand() => new()
    {
        SsoUserIdInt = SsoUserIdInt,
        UserName = @"DEFRA\jdoe",
        FullName = "Jane Internal"
    };

    private static InternalUser ExistingInternalUser(Guid? ssoUserIdInt) => new()
    {
        Id = UserId,
        UserName = @"DEFRA\jdoe",
        FullName = "Jane Internal",
        SsoUserIdInt = ssoUserIdInt,
        IsProfileEditor = false,
        IsPolicyProfileUser = false
    };

    [Fact]
    public async Task ResolveInternalUserAsync_RecordsTheTrimmedEmailAddress_WhenTheMatchedUserHasNone()
    {
        var existing = ExistingInternalUser(SsoUserIdInt);
        repository
            .Setup(repo => repo.GetBySsoUserIdIntAsync(SsoUserIdInt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repository
            .Setup(repo => repo.UpdateEmailAddressAsync(UserId, "jane@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await CreateService().ResolveInternalUserAsync(InternalCommand() with { Email = " jane@example.test " }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Verify(repo => repo.UpdateEmailAddressAsync(UserId, "jane@example.test", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveInternalUserAsync_RecordsTheEmailAddress_WhenTheUserIsMatchedByUserName()
    {
        repository
            .Setup(repo => repo.GetBySsoUserIdIntAsync(SsoUserIdInt, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InternalUser?)null);
        repository
            .Setup(repo => repo.GetByUserNameAsync(@"DEFRA\jdoe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExistingInternalUser(ssoUserIdInt: null));
        repository
            .Setup(repo => repo.UpdateSsoUserIdIntAsync(UserId, SsoUserIdInt, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        repository
            .Setup(repo => repo.UpdateEmailAddressAsync(UserId, "jane@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await CreateService().ResolveInternalUserAsync(InternalCommand() with { Email = "jane@example.test" }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Verify(repo => repo.UpdateEmailAddressAsync(UserId, "jane@example.test", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveInternalUserAsync_RecordsTheEmailAddress_ForANewlyCreatedUser()
    {
        GivenNoInternalUserMatches();
        InternalUser? created = null;
        repository
            .Setup(repo => repo.CreateInternalUserAsync(It.IsAny<InternalUser>(), It.IsAny<CancellationToken>()))
            .Callback<InternalUser, CancellationToken>((user, _) => created = user)
            .ReturnsAsync((InternalUser user, CancellationToken _) => user);
        repository
            .Setup(repo => repo.UpdateEmailAddressAsync(It.IsAny<Guid>(), "jane@example.test", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await CreateService().ResolveInternalUserAsync(InternalCommand() with { Email = "jane@example.test" }, CancellationToken.None);

        repository.Verify(repo => repo.UpdateEmailAddressAsync(created!.Id, "jane@example.test", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveInternalUserAsync_DoesNotReplaceAnEmailAddressTheUserAlreadyHolds()
    {
        var existing = ExistingInternalUser(SsoUserIdInt) with { EmailAddress = "held@example.test" };
        repository
            .Setup(repo => repo.GetBySsoUserIdIntAsync(SsoUserIdInt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateService().ResolveInternalUserAsync(InternalCommand() with { Email = "jane@example.test" }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Verify(repo => repo.UpdateEmailAddressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa@example.test")]
    public async Task ResolveInternalUserAsync_DoesNotRecordAnEmailAddress_WhenThereIsNoUsableOne(string? email)
    {
        repository
            .Setup(repo => repo.GetBySsoUserIdIntAsync(SsoUserIdInt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExistingInternalUser(SsoUserIdInt));

        var result = await CreateService().ResolveInternalUserAsync(InternalCommand() with { Email = email }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        repository.Verify(repo => repo.UpdateEmailAddressAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveInternalUserAsync_StillSignsTheUserIn_WhenRecordingTheEmailAddressFails()
    {
        var existing = ExistingInternalUser(SsoUserIdInt);
        repository
            .Setup(repo => repo.GetBySsoUserIdIntAsync(SsoUserIdInt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repository
            .Setup(repo => repo.UpdateEmailAddressAsync(UserId, "jane@example.test", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FakeDbException("boom"));

        var result = await CreateService().ResolveInternalUserAsync(InternalCommand() with { Email = "jane@example.test" }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(existing);
    }

    [Fact]
    public async Task ResolveInternalUserAsync_ReturnsExistingUser_WhenMatchedBySsoUserIdInt()
    {
        var existing = ExistingInternalUser(SsoUserIdInt);

        repository
            .Setup(repo => repo.GetBySsoUserIdIntAsync(SsoUserIdInt, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await CreateService().ResolveInternalUserAsync(InternalCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(existing);

        repository.Verify(repo => repo.GetByUserNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.UpdateSsoUserIdIntAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(repo => repo.CreateInternalUserAsync(It.IsAny<InternalUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveInternalUserAsync_LinksAndReturnsUser_WhenMatchedByUserName()
    {
        var existing = ExistingInternalUser(ssoUserIdInt: null);

        repository
            .Setup(repo => repo.GetBySsoUserIdIntAsync(SsoUserIdInt, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InternalUser?)null);
        repository
            .Setup(repo => repo.GetByUserNameAsync(@"DEFRA\jdoe", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        repository
            .Setup(repo => repo.UpdateSsoUserIdIntAsync(UserId, SsoUserIdInt, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var result = await CreateService().ResolveInternalUserAsync(InternalCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(UserId);
        result.Value.SsoUserIdInt.Should().Be(SsoUserIdInt);

        repository.Verify(repo => repo.UpdateSsoUserIdIntAsync(UserId, SsoUserIdInt, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(repo => repo.CreateInternalUserAsync(It.IsAny<InternalUser>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveInternalUserAsync_CreatesAUserWithNoPrivileges_WhenNoRowMatches()
    {
        GivenNoInternalUserMatches();

        InternalUser? created = null;
        repository
            .Setup(repo => repo.CreateInternalUserAsync(It.IsAny<InternalUser>(), It.IsAny<CancellationToken>()))
            .Callback<InternalUser, CancellationToken>((user, _) => created = user)
            .ReturnsAsync((InternalUser user, CancellationToken _) => user);

        var result = await CreateService().ResolveInternalUserAsync(InternalCommand(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        created.Should().NotBeNull();
        created.Id.Should().NotBe(Guid.Empty);
        created.UserName.Should().Be(@"DEFRA\jdoe");
        created.FullName.Should().Be("Jane Internal");
        created.SsoUserIdInt.Should().Be(SsoUserIdInt);
        created.IsProfileEditor.Should().BeFalse();
        created.IsPolicyProfileUser.Should().BeFalse();

        result.Value.Should().BeSameAs(created);
        repository.Verify(repo => repo.UpdateSsoUserIdIntAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ResolveInternalUserAsync_UsesTheUserNameAsTheFullName_WhenTheTokenHasNoName(string fullName)
    {
        GivenNoInternalUserMatches();

        InternalUser? created = null;
        repository
            .Setup(repo => repo.CreateInternalUserAsync(It.IsAny<InternalUser>(), It.IsAny<CancellationToken>()))
            .Callback<InternalUser, CancellationToken>((user, _) => created = user)
            .ReturnsAsync((InternalUser user, CancellationToken _) => user);

        await CreateService().ResolveInternalUserAsync(InternalCommand() with { FullName = fullName }, CancellationToken.None);

        created!.FullName.Should().Be(@"DEFRA\jdoe");
    }

    [Fact]
    public async Task ResolveInternalUserAsync_ReturnsTheStoredRow_WhenAConcurrentSignInCreatedItFirst()
    {
        GivenNoInternalUserMatches();

        var stored = ExistingInternalUser(SsoUserIdInt);
        repository
            .Setup(repo => repo.CreateInternalUserAsync(It.IsAny<InternalUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);

        var result = await CreateService().ResolveInternalUserAsync(InternalCommand(), CancellationToken.None);

        result.Value.Should().BeSameAs(stored);
    }

    private void GivenNoInternalUserMatches()
    {
        repository
            .Setup(repo => repo.GetBySsoUserIdIntAsync(SsoUserIdInt, It.IsAny<CancellationToken>()))
            .ReturnsAsync((InternalUser?)null);
        repository
            .Setup(repo => repo.GetByUserNameAsync(@"DEFRA\jdoe", It.IsAny<CancellationToken>()))
            .ReturnsAsync((InternalUser?)null);
    }

    [Fact]
    public async Task ResolveInternalUserAsync_Throws_WhenCommandIsNull()
    {
        var act = async () => await CreateService().ResolveInternalUserAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    private static LookUpUserByEmailCommand LookUp(UserLookupKind kind, string emailAddress = "user@example.com") => new()
    {
        EmailAddress = emailAddress,
        Kind = kind
    };

    private void GivenEmailAddressIsHeldBy(params ExternalUser[] users) =>
        repository
            .Setup(repo => repo.FindByEmailAddressAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

    [Fact]
    public async Task LookUpByEmailAsync_ReturnsNotFound_WhenNoUserHoldsTheAddress()
    {
        GivenEmailAddressIsHeldBy();

        var result = await CreateService().LookUpByEmailAsync(LookUp(UserLookupKind.Internal), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Theory]
    [InlineData(UserLookupKind.Internal)]
    [InlineData(UserLookupKind.External)]
    public async Task LookUpByEmailAsync_ReturnsConflict_WhenSeveralUsersHoldTheAddress(UserLookupKind kind)
    {
        GivenEmailAddressIsHeldBy(ExistingUser(SsoUserId), ExistingUser(SsoUserId) with { Id = Guid.NewGuid() });

        var result = await CreateService().LookUpByEmailAsync(LookUp(kind), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Conflict);
    }

    [Fact]
    public async Task LookUpByEmailAsync_TrimsTheAddressBeforeLookingItUp()
    {
        var user = ExistingUser(ssoUserId: null);
        GivenEmailAddressIsHeldBy(user);

        var result = await CreateService().LookUpByEmailAsync(LookUp(UserLookupKind.Internal, "  user@example.com "), CancellationToken.None);

        result.Value.Should().BeSameAs(user);
    }

    [Fact]
    public async Task LookUpByEmailAsync_ReturnsAnInternalUser_ForAnInternalSignIn()
    {
        var user = ExistingUser(ssoUserId: null) with { SsoUserIdInt = SsoUserIdInt, IsProfileEditor = true };
        GivenEmailAddressIsHeldBy(user);

        var result = await CreateService().LookUpByEmailAsync(LookUp(UserLookupKind.Internal), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(user);
    }

    [Fact]
    public async Task LookUpByEmailAsync_ReturnsForbidden_WhenAnExternalUserIsAskedForAnInternalSignIn()
    {
        GivenEmailAddressIsHeldBy(ExistingUser(SsoUserId));

        var result = await CreateService().LookUpByEmailAsync(LookUp(UserLookupKind.Internal), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbidden);
        result.Error.Should().NotContain("user@example.com");
    }

    [Fact]
    public async Task LookUpByEmailAsync_ReturnsAnExternalUser_ForAnExternalSignIn()
    {
        var user = ExistingUser(SsoUserId);
        GivenEmailAddressIsHeldBy(user);

        var result = await CreateService().LookUpByEmailAsync(LookUp(UserLookupKind.External), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(user);
    }

    [Fact]
    public async Task LookUpByEmailAsync_ReturnsAnInternalProfileEditor_ForAnExternalSignIn()
    {
        var user = ExistingUser(ssoUserId: null) with { SsoUserIdInt = SsoUserIdInt, IsProfileEditor = true };
        GivenEmailAddressIsHeldBy(user);

        var result = await CreateService().LookUpByEmailAsync(LookUp(UserLookupKind.External), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(user);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    public async Task LookUpByEmailAsync_ReturnsForbidden_WhenAnInternalUserWhoIsNotAProfileEditorIsAskedForAnExternalSignIn(bool hasSsoUserIdInt, bool isProfileEditor)
    {
        // An internal row that is not a profile editor, or that has never signed in through Entra ID, has no way in through CIDM either.
        var user = ExistingUser(ssoUserId: null) with
        {
            SsoUserIdInt = hasSsoUserIdInt ? SsoUserIdInt : null,
            IsProfileEditor = isProfileEditor
        };
        GivenEmailAddressIsHeldBy(user);

        var result = await CreateService().LookUpByEmailAsync(LookUp(UserLookupKind.External), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbidden);
    }

    [Fact]
    public async Task LookUpByEmailAsync_Throws_WhenCommandIsNull()
    {
        var act = async () => await CreateService().LookUpByEmailAsync(null!, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }
}
