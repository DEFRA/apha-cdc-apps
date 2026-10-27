using CDC.Api.Application;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.Users.Interfaces;
using CDC.Api.Middleware;
using CDC.Common.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;

namespace CDC.Api.Tests.Middleware;

public class ActingUserMiddlewareTests
{
    private static readonly Guid UserId = Guid.Parse("88d1e0c6-7d10-427d-a82f-33fc2c1cef28");

    private readonly Mock<IUserRepository> users = new(MockBehavior.Strict);
    private readonly RequestUserContext userContext = new();

    [Fact]
    public async Task InvokeAsync_LoadsTheActingUsersRoleFlagsFromTheDatabase()
    {
        users
            .Setup(repo => repo.GetActingUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActingUser { Id = UserId, IsProfileEditor = true, IsPolicyProfileUser = false, SsoUserIdInt = Guid.NewGuid() });
        var nextCalled = false;

        await CreateMiddleware(_ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            })
            .InvokeAsync(CreateContext(UserId.ToString()), userContext, users.Object);

        nextCalled.Should().BeTrue();
        userContext.UserId.Should().Be(UserId);
        userContext.IsProfileEditor.Should().BeTrue();
        userContext.IsPolicyProfileUser.Should().BeFalse();
    }

    [Fact]
    public async Task InvokeAsync_TakesTheFlagsFromTheDatabase_NeverFromTheRequest()
    {
        users
            .Setup(repo => repo.GetActingUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActingUser { Id = UserId, IsProfileEditor = false, IsPolicyProfileUser = true, SsoUserIdInt = Guid.NewGuid() });
        var context = CreateContext(UserId.ToString());
        context.Request.Headers["X-Is-Profile-Editor"] = "true";

        await CreateMiddleware(_ => Task.CompletedTask).InvokeAsync(context, userContext, users.Object);

        userContext.IsProfileEditor.Should().BeFalse();
        userContext.IsPolicyProfileUser.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("{88d1e0c6-7d10-427d-a82f-33fc2c1cef28}")]
    public async Task InvokeAsync_LeavesTheRequestAnonymous_WhenTheHeaderIsMissingOrNotAGuid(string? header)
    {
        var nextCalled = false;

        await CreateMiddleware(_ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            })
            .InvokeAsync(CreateContext(header), userContext, users.Object);

        nextCalled.Should().BeTrue();
        AssertAnonymous();
    }

    [Fact]
    public async Task InvokeAsync_LeavesTheRequestAnonymous_WhenTheHeaderIsRepeated()
    {
        var context = CreateContext(null);
        context.Request.Headers[ActingUserHeader.Name] = new StringValues([UserId.ToString(), Guid.NewGuid().ToString()]);

        await CreateMiddleware(_ => Task.CompletedTask).InvokeAsync(context, userContext, users.Object);

        AssertAnonymous();
    }

    [Fact]
    public async Task InvokeAsync_LeavesTheRequestAnonymous_WhenNoSuchUserExists()
    {
        users
            .Setup(repo => repo.GetActingUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ActingUser?)null);
        var nextCalled = false;

        await CreateMiddleware(_ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            })
            .InvokeAsync(CreateContext(UserId.ToString()), userContext, users.Object);

        nextCalled.Should().BeTrue();
        AssertAnonymous();
    }

    [Fact]
    public async Task InvokeAsync_PropagatesAFailureToLoadTheUser()
    {
        users
            .Setup(repo => repo.GetActingUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var act = async () => await CreateMiddleware(_ => Task.CompletedTask)
            .InvokeAsync(CreateContext(UserId.ToString()), userContext, users.Object);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    private void AssertAnonymous()
    {
        userContext.UserId.Should().BeNull();
        userContext.IsProfileEditor.Should().BeFalse();
        userContext.IsPolicyProfileUser.Should().BeFalse();
        users.VerifyAll();
    }

    private static DefaultHttpContext CreateContext(string? actingUserHeader)
    {
        var context = new DefaultHttpContext();

        if (actingUserHeader is not null)
        {
            context.Request.Headers[ActingUserHeader.Name] = actingUserHeader;
        }

        return context;
    }

    private static ActingUserMiddleware CreateMiddleware(RequestDelegate next) =>
        new(next, NullLogger<ActingUserMiddleware>.Instance);
}
