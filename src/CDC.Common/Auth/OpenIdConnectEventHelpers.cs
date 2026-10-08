using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.DependencyInjection;

namespace CDC.Common.Auth;

/// <summary>
/// Shared OpenID Connect event logic for identity-provider-specific handlers (CIDM, Entra ID, ...),
/// so each only needs to supply its own resolver interface/resolution type and failure logging.
/// </summary>
public static class OpenIdConnectEventHelpers
{
    /// <summary>
    /// Resolves the signed-in identity against an optional, per-request <typeparamref name="TResolver"/>
    /// (resolved from <c>HttpContext.RequestServices</c>, not constructor-injected, since the OIDC
    /// events class is a singleton), denying sign-in and redirecting when resolution disallows it, or
    /// adding its claims to <paramref name="identity"/> when allowed. A no-op if the consuming app has
    /// not registered a <typeparamref name="TResolver"/>.
    /// </summary>
    public static async Task ResolveOrDenyAsync<TResolver, TResolution>(
        TokenValidatedContext context,
        ClaimsIdentity identity,
        Func<TResolver, ClaimsPrincipal, CancellationToken, Task<TResolution>> resolve)
        where TResolver : class
        where TResolution : IIdentityResolution
    {
        var resolver = context.HttpContext.RequestServices?.GetService<TResolver>();
        if (resolver is null || context.Principal is null)
        {
            return;
        }

        var resolution = await resolve(resolver, context.Principal, context.HttpContext.RequestAborted);
        if (!resolution.IsAllowed)
        {
            // Runs before the OIDC handler ever signs the principal into the cookie scheme, so
            // denying here means no local session is ever created for this sign-in attempt.
            context.HandleResponse();
            context.HttpContext.Response.Redirect(resolution.DenialRedirectPath ?? "/");
            return;
        }

        foreach (var (claimType, claimValue) in resolution.Claims ?? new Dictionary<string, string>())
        {
            identity.AddClaim(new Claim(claimType, claimValue));
        }
    }

    /// <summary>
    /// Logs the failure server-side only and redirects to the generic error page - never surfaces
    /// raw OIDC failure detail to the user.
    /// </summary>
    public static Task HandleRemoteFailureAsync(RemoteFailureContext context, Action<Exception?> logFailure)
    {
        logFailure(context.Failure);
        context.HandleResponse();
        context.Response.Redirect("/Landing/Error");
        return Task.CompletedTask;
    }
}
