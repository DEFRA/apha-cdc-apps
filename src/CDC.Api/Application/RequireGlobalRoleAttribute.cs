using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CDC.Api.Application;

/// <summary>The global roles an internal user can hold.</summary>
public enum GlobalRole
{
    /// <summary>The profile editor, who can do everything.</summary>
    ProfileEditor,

    /// <summary>The policy profile user, who can read every draft and run every report.</summary>
    PolicyProfileUser
}

/// <summary>
/// Refuses a request unless the acting user holds one of the named global roles. The API has no sign-in of its own - CDC.Web
/// names the user in a header and <c>ActingUserMiddleware</c> reads their roles from the database - so this is what keeps an
/// endpoint that only an administrator should reach from being called by anyone who is merely signed in.
/// A refused request is an <see cref="UnauthorizedAccessException"/>, which the global exception handler turns into a 403.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
public sealed class RequireGlobalRoleAttribute : TypeFilterAttribute
{
    /// <summary>Initialises a new instance of the <see cref="RequireGlobalRoleAttribute"/> class.</summary>
    /// <param name="roles">The roles that may call the endpoint; holding any one is enough.</param>
    public RequireGlobalRoleAttribute(params GlobalRole[] roles)
        : base(typeof(GlobalRoleFilter))
    {
        ArgumentNullException.ThrowIfNull(roles);

        Arguments = [roles];
    }
}

/// <summary>The filter behind <see cref="RequireGlobalRoleAttribute"/>.</summary>
/// <param name="roles">The roles that may call the endpoint.</param>
/// <param name="userContext">The acting user.</param>
public sealed class GlobalRoleFilter(GlobalRole[] roles, IUserContext userContext) : IAuthorizationFilter
{
    /// <inheritdoc />
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (!roles.Any(Holds))
        {
            throw new UnauthorizedAccessException($"The acting user does not hold the {string.Join(" or ", roles)} role.");
        }
    }

    private bool Holds(GlobalRole role) => role switch
    {
        GlobalRole.ProfileEditor => ProfileAccess.IsEditor(userContext),
        GlobalRole.PolicyProfileUser => ProfileAccess.IsPolicyUser(userContext),
        _ => false
    };
}
