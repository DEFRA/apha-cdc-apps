using System.Security.Claims;
using CDC.Auth.Cidm;
using CDC.Auth.Cidm.Events;
using CDC.Auth.Entra;
using CDC.Auth.Entra.Claims;
using CDC.Auth.Entra.Events;
using CDC.Common.Auth;
using CDC.Web.Features.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace CDC.Web.Features.LoadTesting;

/// <summary>Signs a user in without the identity provider. Registered only while LoadTesting is switched on.</summary>
public interface ILoadTestSignIn
{
    /// <summary>Gets a value indicating whether sign-in is bypassed right now.</summary>
    bool IsActive { get; }

    /// <summary>Signs in the configured internal user.</summary>
    /// <param name="httpContext">The sign-in request.</param>
    /// <returns>Whether the user was signed in, and where to send them when not.</returns>
    Task<LoadTestSignInResult> SignInInternalAsync(HttpContext httpContext);

    /// <summary>Signs in the configured external user.</summary>
    /// <param name="httpContext">The sign-in request.</param>
    /// <returns>Whether the user was signed in, and where to send them when not.</returns>
    Task<LoadTestSignInResult> SignInExternalAsync(HttpContext httpContext);
}

/// <summary>Outcome of <see cref="ILoadTestSignIn"/>.</summary>
/// <param name="Succeeded">Whether the user was signed in.</param>
/// <param name="DenialRedirectPath">The local path to redirect to when the user was not signed in.</param>
public sealed record LoadTestSignInResult(bool Succeeded, string? DenialRedirectPath = null)
{
    /// <summary>Gets the outcome of a user who was signed in.</summary>
    public static LoadTestSignInResult Allowed { get; } = new(true);

    /// <summary>Creates the outcome of a user who was refused.</summary>
    /// <param name="redirectPath">The local path to redirect to instead.</param>
    /// <returns>The refusal.</returns>
    public static LoadTestSignInResult Denied(string redirectPath) => new(false, redirectPath);
}

/// <summary>
/// Default <see cref="ILoadTestSignIn"/>. It hands the real resolvers the claims a token from the identity provider would carry, so
/// the user's row, roles and claims come from CDC.Api exactly as they do after a real sign-in, then signs the result into the
/// same session cookie the identity provider's callback would have.
/// </summary>
/// <param name="options">The LoadTesting configuration.</param>
/// <param name="status">Whether the bypass is active.</param>
/// <param name="internalResolver">Resolves the internal user, as after an Entra ID sign-in.</param>
/// <param name="externalResolver">Resolves the external user, as after a CIDM sign-in.</param>
/// <param name="logger">Logs each sign-in by opaque id.</param>
public sealed class LoadTestSignIn(
    IOptions<LoadTestingOptions> options,
    ILoadTestingStatus status,
    IEntraInternalUserResolver internalResolver,
    ICidmExternalUserResolver externalResolver,
    ILogger<LoadTestSignIn> logger) : ILoadTestSignIn
{
    private const string InternalKind = "internal";
    private const string ExternalKind = "external";

    /// <inheritdoc />
    public bool IsActive => status.IsActive;

    /// <inheritdoc />
    public async Task<LoadTestSignInResult> SignInInternalAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var settings = options.Value;
        var ssoUserId = settings.InternalSsoUserId.ToString();

        // Only the claims InternalUserResolver reads from an Entra ID id_token.
        var token = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(EntraClaimTypes.ObjectId, ssoUserId),
            new Claim(EntraClaimTypes.OnPremisesSamAccountName, settings.InternalUserName),
            new Claim(ClaimTypes.Name, settings.InternalUserName)
        ]));

        var resolution = await internalResolver.ResolveAsync(token, httpContext.RequestAborted);

        return await CompleteAsync(httpContext, EntraAuthenticationDefaults.CookieAuthenticationScheme, InternalKind, ssoUserId, settings.InternalUserName, resolution);
    }

    /// <inheritdoc />
    public async Task<LoadTestSignInResult> SignInExternalAsync(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        var settings = options.Value;
        var ssoUserId = settings.ExternalSsoUserId.ToString();

        // Only the claims ExternalUserResolver reads from a CIDM id_token; the names and organisation matter only to a new row.
        var token = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, ssoUserId),
            new Claim(ClaimTypes.Email, settings.ExternalUserEmail)
        ]));

        var resolution = await externalResolver.ResolveAsync(token, httpContext.RequestAborted);

        return await CompleteAsync(httpContext, CookieAuthenticationDefaults.AuthenticationScheme, ExternalKind, ssoUserId, settings.ExternalUserEmail, resolution);
    }

    private async Task<LoadTestSignInResult> CompleteAsync(
        HttpContext httpContext,
        string cookieScheme,
        string kind,
        string ssoUserId,
        string fallbackName,
        IIdentityResolution resolution)
    {
        if (!resolution.IsAllowed)
        {
            var redirectPath = resolution.DenialRedirectPath ?? "/";
            logger.SignInDenied(kind, redirectPath);

            return LoadTestSignInResult.Denied(redirectPath);
        }

        var resolved = resolution.Claims ?? new Dictionary<string, string>();
        var userId = resolved.GetValueOrDefault(ExternalUserClaimTypes.InternalUserId) ?? string.Empty;
        var claims = resolved.Select(claim => new Claim(claim.Key, claim.Value)).ToList();
        claims.Add(new Claim(ClaimTypes.NameIdentifier, ssoUserId));
        claims.Add(new Claim(ClaimTypes.Name, resolved.GetValueOrDefault(ExternalUserClaimTypes.FullName) ?? fallbackName));
        claims.Add(new Claim(LoadTestingDefaults.SessionClaimType, bool.TrueString));

        await httpContext.SignInAsync(cookieScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, LoadTestingDefaults.AuthenticationType)));
        logger.SignedIn(kind, userId);

        return LoadTestSignInResult.Allowed;
    }
}
