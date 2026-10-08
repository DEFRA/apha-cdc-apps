using System.Security.Claims;
using CDC.Web.Authorization.Constants;
using CDC.Web.Authorization.Handlers;
using CDC.Web.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace CDC.Web.Tests.Authorization;

public class AllowedRolesAuthorizationHandlerTests
{
    private static readonly AllowedRolesAuthorizationHandler Handler = new();

    [Fact]
    public async Task HandleAsync_Succeeds_WhenUserIsInAnAllowedRole()
    {
        var requirement = new AllowedRolesRequirement(AuthorizationRoles.ProfileEditor, AuthorizationRoles.Reviewer);
        var context = CreateContext(requirement, AuthorizationRoles.Reviewer);

        await Handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_Fails_WhenUserIsInNoAllowedRole()
    {
        var requirement = new AllowedRolesRequirement(AuthorizationRoles.ProfileEditor);
        var context = CreateContext(requirement, AuthorizationRoles.Contributor);

        await Handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_Fails_WhenUserHasNoRoles()
    {
        var requirement = new AllowedRolesRequirement(AuthorizationRoles.ProfileEditor);
        var context = CreateContext(requirement);

        await Handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task HandleAsync_Fails_ForUserManagementSystem_EvenWhenAlsoInAnAllowedRole()
    {
        // Mirrors legacy ProfilesIdentity.CanBeUserManagementSystem(): this combination can't
        // occur in practice, but the handler must still fail closed if it ever did.
        var requirement = new AllowedRolesRequirement(AuthorizationRoles.ProfileEditor);
        var context = CreateContext(requirement, AuthorizationRoles.ProfileEditor, AuthorizationRoles.UserManagementSystem);

        await Handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public void Constructor_Throws_WhenNoAllowedRolesAreGiven()
    {
        Assert.Throws<ArgumentException>(() => new AllowedRolesRequirement());
    }

    private static AuthorizationHandlerContext CreateContext(AllowedRolesRequirement requirement, params string[] roles)
    {
        var identity = new ClaimsIdentity(
            roles.Select(role => new Claim(ClaimTypes.Role, role)),
            authenticationType: "Test");

        return new AuthorizationHandlerContext([requirement], new ClaimsPrincipal(identity), resource: null);
    }
}
