using CDC.Auth.Entra;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace CDC.Web.Features.Account;

/// <summary>Registers the session timeout that both sign-in cookies share.</summary>
public static class SessionTimeoutServiceCollectionExtensions
{
    /// <summary>
    /// Binds and validates the <c>SessionTimeout</c> section, and makes its <see cref="SessionTimeoutOptions.IdleTimeout"/> the lifetime of
    /// the CIDM and Entra ID sign-in cookies. The cookies' events renew a session on activity, so the lifetime is an idle timeout.
    /// </summary>
    /// <param name="services">The service collection; the identity providers' cookie schemes must be registered on it.</param>
    /// <param name="configuration">The configuration holding the <c>SessionTimeout</c> section.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddSessionTimeout(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SessionTimeoutOptions>()
            .Bind(configuration.GetSection(SessionTimeoutOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<SessionTimeoutOptions>, SessionTimeoutOptionsValidator>();

        foreach (var cookieScheme in new[] { CookieAuthenticationDefaults.AuthenticationScheme, EntraAuthenticationDefaults.CookieAuthenticationScheme })
        {
            services.AddOptions<CookieAuthenticationOptions>(cookieScheme)
                .Configure<IOptions<SessionTimeoutOptions>>((cookie, sessionTimeout) => cookie.ExpireTimeSpan = sessionTimeout.Value.IdleTimeout);
        }

        return services;
    }
}
