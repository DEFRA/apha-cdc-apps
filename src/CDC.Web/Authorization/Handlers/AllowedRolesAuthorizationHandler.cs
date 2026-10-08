using CDC.Web.Authorization.Constants;
using CDC.Web.Authorization.Requirements;
using Microsoft.AspNetCore.Authorization;

namespace CDC.Web.Authorization.Handlers;

/// <summary>
/// Satisfies <see cref="AllowedRolesRequirement"/> when the user is in one of its allowed roles.
/// A user in the <see cref="AuthorizationRoles.UserManagementSystem"/> role is always denied,
/// regardless of any other role they also hold - mirroring every legacy <c>CanXxx()</c> method's
/// <c>AndAlso Not identity.IsUserManagementSystem</c> guard, and the explicit requirement that
/// this role is denied every navigation item.
/// </summary>
public sealed class AllowedRolesAuthorizationHandler : AuthorizationHandler<AllowedRolesRequirement>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AllowedRolesRequirement requirement)
    {
        if (context.User.IsInRole(AuthorizationRoles.UserManagementSystem))
        {
            return Task.CompletedTask;
        }

        if (requirement.AllowedRoles.Any(context.User.IsInRole))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
