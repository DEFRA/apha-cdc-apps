using System.Security.Claims;
using System.Text.Encodings.Web;
using CDC.Common.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CDC.Api.Authentication;

/// <summary>
/// Authenticates the calling service, not a person. CDC.Web sends the shared key held in Secrets Manager in the
/// <see cref="ServiceKeyHeader"/> header on every call; any other caller is refused with a 401 by the fallback
/// authorization policy. Only an authenticated caller may name an acting user, whose roles still come from the
/// database (see <c>ActingUserMiddleware</c>).
/// </summary>
/// <param name="options">The scheme options.</param>
/// <param name="logger">Logger factory used by the base handler.</param>
/// <param name="encoder">URL encoder used by the base handler.</param>
/// <param name="configuration">Configuration holding the expected key.</param>
public sealed class ServiceKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IConfiguration configuration)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>Name of the authentication scheme.</summary>
    public const string SchemeName = "ServiceKey";

    /// <summary>Configuration key of the expected service key (environment variable <c>ServiceAuth__ApiKey</c>).</summary>
    public const string ConfigurationKey = "ServiceAuth:ApiKey";

    /// <summary>The name given to an authenticated caller.</summary>
    public const string CallerName = "CDC.Web";

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ServiceKeyHeader.Name, out var values))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var expectedKey = configuration[ConfigurationKey];

        if (values.Count != 1 || string.IsNullOrEmpty(expectedKey) || !SharedSecret.Matches(values[0], expectedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("The service key is not valid."));
        }

        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, CallerName)], SchemeName);

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}

/// <summary>Registers <see cref="ServiceKeyAuthenticationHandler"/> and makes it required on every endpoint.</summary>
public static class ServiceKeyAuthenticationExtensions
{
    /// <summary>
    /// Adds the service-key scheme as the default and a fallback policy that requires an authenticated caller, so an
    /// endpoint is protected unless it opts out with <c>AllowAnonymous</c> (only <c>/health</c> and <c>/health/ready</c> do).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddServiceKeyAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(ServiceKeyAuthenticationHandler.SchemeName)
            .AddScheme<AuthenticationSchemeOptions, ServiceKeyAuthenticationHandler>(ServiceKeyAuthenticationHandler.SchemeName, configureOptions: null);

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder(ServiceKeyAuthenticationHandler.SchemeName).RequireAuthenticatedUser().Build());

        return services;
    }
}
