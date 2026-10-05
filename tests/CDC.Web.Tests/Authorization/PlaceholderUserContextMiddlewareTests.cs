using CDC.Web.Authorization.Middleware;
using CDC.Web.Authorization.Services;
using Microsoft.AspNetCore.Http;

namespace CDC.Web.Tests.Authorization;

public class PlaceholderUserContextMiddlewareTests
{
    [Fact]
    public async Task InvokeAsync_SetsHttpContextUser_FromCurrentUserService_AndCallsNext()
    {
        var currentUserService = new FakeCurrentUserService("test-user", ["ProfileEditor", "Reviewer"]);
        var nextCalled = false;
        var middleware = new PlaceholderUserContextMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();

        await middleware.InvokeAsync(context, currentUserService);

        Assert.True(nextCalled);
        Assert.True(context.User.IsInRole("ProfileEditor"));
        Assert.True(context.User.IsInRole("Reviewer"));
        Assert.False(context.User.IsInRole("UserManagementSystem"));
        Assert.Equal("test-user", context.User.Identity?.Name);
    }

    private sealed class FakeCurrentUserService(string userId, IReadOnlyList<string> roles) : ICurrentUserService
    {
        public string UserId => userId;
        public IReadOnlyList<string> Roles => roles;
    }
}
