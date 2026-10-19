using CDC.Api.Application;
using CDC.Api.Domain.Entities;
using FluentAssertions;

namespace CDC.Api.Tests.Application;

public class RequestUserContextTests
{
    private static readonly Guid UserId = Guid.Parse("88d1e0c6-7d10-427d-a82f-33fc2c1cef28");

    [Fact]
    public void ARequestWithNoActingUser_IsAnonymous_WithNoRole()
    {
        var context = new RequestUserContext();

        context.UserId.Should().BeNull();
        context.IsProfileEditor.Should().BeFalse();
        context.IsPolicyProfileUser.Should().BeFalse();
        context.IsUserManagementSystem.Should().BeFalse();
    }

    [Fact]
    public void SetActingUser_ExposesTheUsersIdAndRoleFlags()
    {
        var context = new RequestUserContext();

        context.SetActingUser(new ActingUser { Id = UserId, IsProfileEditor = true, IsPolicyProfileUser = true });

        context.UserId.Should().Be(UserId);
        context.IsProfileEditor.Should().BeTrue();
        context.IsPolicyProfileUser.Should().BeTrue();
        context.IsUserManagementSystem.Should().BeFalse();
    }

    [Fact]
    public void RequireUserId_ReturnsTheActingUsersId()
    {
        var context = new RequestUserContext();
        context.SetActingUser(new ActingUser { Id = UserId, IsProfileEditor = false, IsPolicyProfileUser = false });

        context.RequireUserId().Should().Be(UserId);
    }

    [Fact]
    public void RequireUserId_Throws_WhenTheRequestIsAnonymous()
    {
        var act = () => new RequestUserContext().RequireUserId();

        act.Should().Throw<UnauthorizedAccessException>();
    }
}
