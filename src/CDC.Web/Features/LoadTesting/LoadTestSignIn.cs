using System.Security.Claims;
using CDC.Auth.Entra;
using CDC.Web.Features.Account;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Polly;

namespace CDC.Web.Features.LoadTesting;

/// <summary>Signs a user in without the identity provider. Registered only while LoadTesting is switched on.</summary>
public interface ILoadTestSignIn
{
    /// <summary>Gets a value indicating whether sign-in is bypassed right now.</summary>
    bool IsActive { get; }

    /// <summary>
    /// Signs in the user who holds an email address, when that user could sign in as the kind of user asked for. The user's row,
    /// roles and claims come from CDC.Api, so every authorisation check afterwards is made for that user.
    /// </summary>
    /// <param name="httpContext">The sign-in request.</param>
    /// <param name="kind">The kind of sign-in the user chose: internal or external.</param>
    /// <param name="emailAddress">The email address of the user to sign in as.</param>
    /// <returns>Whether the user was signed in and, when not, why not.</returns>
    Task<LoadTestSignInOutcome> SignInAsync(HttpContext httpContext, UserLookupKind kind, string emailAddress);
}

/// <summary>Outcome of <see cref="ILoadTestSignIn.SignInAsync"/>.</summary>
public enum LoadTestSignInOutcome
{
    /// <summary>The user was signed in.</summary>
    SignedIn,

    /// <summary>No user holds the email address.</summary>
    NotFound,

    /// <summary>The user holding the email address could not sign in as that kind of user.</summary>
    NotPermitted,

    /// <summary>Several users hold the email address, so none can be chosen.</summary>
    Ambiguous,

    /// <summary>The text is not an email address.</summary>
    InvalidEmailAddress,

    /// <summary>CDC.Api could not be reached or did not answer as expected.</summary>
    Unavailable
}

/// <summary>
/// Default <see cref="ILoadTestSignIn"/>. It asks CDC.Api for the user holding the email address, builds the claims the identity
/// providers' sign-ins would have added for that user, and signs the result into the same session cookie the identity provider's
/// callback would have. Nothing is created or changed in the database, so a user who has never signed in for real can still be
/// signed in as, and no identifier is invented for them.
/// </summary>
/// <param name="apiClient">Looks the user up in CDC.Api.</param>
/// <param name="status">Whether the bypass is active.</param>
/// <param name="logger">Logs each sign-in by opaque id.</param>
public sealed class LoadTestSignIn(
    IApiClient apiClient,
    ILoadTestingStatus status,
    ILogger<LoadTestSignIn> logger) : ILoadTestSignIn
{
    private const string InternalKind = "internal";
    private const string ExternalKind = "external";

    /// <inheritdoc />
    public bool IsActive => status.IsActive;

    /// <inheritdoc />
    public async Task<LoadTestSignInOutcome> SignInAsync(HttpContext httpContext, UserLookupKind kind, string emailAddress)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);

        var kindName = kind == UserLookupKind.Internal ? InternalKind : ExternalKind;

        UserLookupResult lookup;
        try
        {
            lookup = await apiClient.LookUpUserByEmailAsync(kind, emailAddress.Trim(), httpContext.RequestAborted);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or NotSupportedException or ExecutionRejectedException)
        {
            // A network failure, a cancelled call, an open circuit or a timeout in the resilience handler, or a reply that is not the
            // expected JSON: all mean CDC.Api could not answer, which is not the same as the user not existing.
            logger.LookupFailed(kindName, exception);

            return LoadTestSignInOutcome.Unavailable;
        }

        if (lookup is not { Outcome: UserLookupOutcome.Found, User: { } user })
        {
            var outcome = lookup.Outcome switch
            {
                UserLookupOutcome.NotFound => LoadTestSignInOutcome.NotFound,
                UserLookupOutcome.NotPermitted => LoadTestSignInOutcome.NotPermitted,
                UserLookupOutcome.Ambiguous => LoadTestSignInOutcome.Ambiguous,
                UserLookupOutcome.InvalidEmailAddress => LoadTestSignInOutcome.InvalidEmailAddress,
                _ => LoadTestSignInOutcome.Unavailable
            };
            logger.SignInRefused(kindName, outcome);

            return outcome;
        }

        var cookieScheme = kind == UserLookupKind.Internal
            ? EntraAuthenticationDefaults.CookieAuthenticationScheme
            : CookieAuthenticationDefaults.AuthenticationScheme;
        var resolved = kind == UserLookupKind.Internal
            ? ResolvedUserClaims.ForInternalUser(user.Id, user.FullName, user.IsProfileEditor, user.IsPolicyProfileUser)
            : ResolvedUserClaims.ForExternalUser(user.Id, user.FullName, user.IsInternalProfileEditor);

        var claims = resolved.Select(claim => new Claim(claim.Key, claim.Value)).ToList();
        claims.Add(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
        claims.Add(new Claim(ClaimTypes.Name, user.FullName));
        claims.Add(new Claim(LoadTestingDefaults.SessionClaimType, bool.TrueString));

        await httpContext.SignInAsync(cookieScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, LoadTestingDefaults.AuthenticationType)));
        logger.SignedIn(kindName, user.Id);

        return LoadTestSignInOutcome.SignedIn;
    }
}
