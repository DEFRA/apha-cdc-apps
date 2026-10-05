using System.Security.Claims;
using CDC.Web.Authorization.Constants;
using CDC.Web.Authorization.Services;

namespace CDC.Web.Authorization.Middleware;

/// <summary>
/// TEMPORARY: builds <c>HttpContext.User</c> from <see cref="ICurrentUserService"/> so every
/// request simulates an authenticated user until real authentication (Entra ID SAML) is
/// integrated. Delete this middleware and <see cref="CurrentUserService"/> once that lands - no
/// policy, handler, or <see cref="Services.INavigationAuthorizationService"/> code needs to
/// change, since they only ever see a populated <see cref="ClaimsPrincipal"/>.
/// </summary>
/// <param name="next">The next middleware in the pipeline.</param>
public sealed class PlaceholderUserContextMiddleware(RequestDelegate next)
{
    /// <summary>Invokes the middleware.</summary>
    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUserService)
    {
        List<Claim> claims =
        [
            new Claim(ClaimTypes.NameIdentifier, currentUserService.UserId),
            new Claim(ClaimTypes.Name, currentUserService.UserId),
            .. currentUserService.Roles.Select(role => new Claim(ClaimTypes.Role, role))
        ];

        var identity = new ClaimsIdentity(claims, authenticationType: nameof(PlaceholderUserContextMiddleware));
        context.User = new ClaimsPrincipal(identity);

        await next(context);
    }
}

/// <summary>Registers <see cref="PlaceholderUserContextMiddleware"/>.</summary>
public static class PlaceholderUserContextMiddlewareExtensions
{
    /// <summary>Adds the temporary placeholder user context middleware to the pipeline.</summary>
    public static IApplicationBuilder UsePlaceholderUserContext(this IApplicationBuilder app) =>
        app.UseMiddleware<PlaceholderUserContextMiddleware>();
}
