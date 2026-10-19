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
    private const string AccessTokenName = "access_token";

    // Claims every provider's app code reads after sign-in; each provider adds its own on top via RetainedClaims.
    private static readonly string[] CommonRetainedClaimTypes =
    [
        ClaimTypes.NameIdentifier,
        ClaimTypes.Name,
        ClaimTypes.GivenName,
        ClaimTypes.Surname,
        ClaimTypes.Email,
        "sub",
        "name",
        "given_name",
        "family_name",
        "email"
    ];

    /// <summary>
    /// Builds the allow-list of claim types kept on the session cookie: the common identity claims plus the
    /// provider-specific ones the app also reads. Everything else only bloats the cookie past the WAF's 10 KB
    /// cookie-header limit.
    /// </summary>
    public static IReadOnlySet<string> RetainedClaims(params string[] providerSpecificClaimTypes)
    {
        var retained = new HashSet<string>(CommonRetainedClaimTypes, StringComparer.Ordinal);
        retained.UnionWith(providerSpecificClaimTypes);
        return retained;
    }

    /// <summary>
    /// Resolves the signed-in identity against an optional, per-request <typeparamref name="TResolver"/>
    /// (resolved from <c>HttpContext.RequestServices</c>, not constructor-injected, since the OIDC
    /// events class is a singleton), denying sign-in and redirecting when resolution disallows it, or
    /// adding its claims to <paramref name="identity"/> when allowed. A no-op if the consuming app has
    /// not registered a <typeparamref name="TResolver"/>. Unless sign-in was denied, claims present before
    /// resolution whose type is not in <paramref name="retainedClaimTypes"/> are then removed (claims the
    /// resolver adds are kept).
    /// </summary>
    public static async Task ResolveOrDenyAsync<TResolver, TResolution>(
        TokenValidatedContext context,
        ClaimsIdentity identity,
        Func<TResolver, ClaimsPrincipal, CancellationToken, Task<TResolution>> resolve,
        IReadOnlySet<string> retainedClaimTypes)
        where TResolver : class
        where TResolution : IIdentityResolution
    {
        var claimsBeforeResolution = identity.Claims.ToList();

        var resolver = context.HttpContext.RequestServices?.GetService<TResolver>();
        if (resolver is not null && context.Principal is not null)
        {
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

        foreach (var claim in claimsBeforeResolution.Where(claim => !retainedClaimTypes.Contains(claim.Type)))
        {
            identity.RemoveClaim(claim);
        }
    }

    /// <summary>
    /// Drops the access token from the tokens saved on the session cookie. The apps never call an API with it,
    /// and it is the largest avoidable part of the cookie, which must stay under the WAF's 10 KB cookie-header limit.
    /// </summary>
    public static Task RemoveAccessTokenAsync(TicketReceivedContext context)
    {
        if (context.Properties is { } properties)
        {
            properties.StoreTokens(properties.GetTokens().Where(token => token.Name != AccessTokenName).ToList());
        }

        return Task.CompletedTask;
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
