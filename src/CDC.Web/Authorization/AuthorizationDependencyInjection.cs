using CDC.Web.Authorization.Models;
using CDC.Web.Authorization.Policies;
using CDC.Web.Authorization.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CDC.Web.Authorization;

/// <summary>
/// Registers the authorization framework: settings, the temporary current-user service, the
/// navigation authorization service, the allowed-roles handler, and every policy. One call from
/// <c>Program.cs</c> - see <c>src/CDC.Web/Program.cs</c> for pipeline registration
/// (<c>app.UsePlaceholderUserContext()</c> before <c>app.UseAuthorization()</c>).
/// </summary>
public static class AuthorizationDependencyInjection
{
    /// <summary>Adds the authorization framework to the container.</summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The application configuration, for <see cref="AuthorizationSettings"/>.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddCdcWebAuthorization(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddOptions<AuthorizationSettings>()
            .Bind(configuration.GetSection(AuthorizationSettings.SectionName));

        services.AddSingleton<ICurrentUserService, CurrentUserService>();
        services.AddScoped<INavigationAuthorizationService, NavigationAuthorizationService>();
        services.AddSingleton<IAuthorizationHandler, Handlers.AllowedRolesAuthorizationHandler>();

        services.AddAuthorizationBuilder();
        services.Configure<AuthorizationOptions>(options => options.AddCdcWebPolicies());

        return services;
    }
}
