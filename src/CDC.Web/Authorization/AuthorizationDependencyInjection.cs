using CDC.Web.Authorization.Policies;
using CDC.Web.Authorization.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

namespace CDC.Web.Authorization;

/// <summary>
/// Registers the authorization framework: the navigation authorization service, the
/// allowed-roles handler, the internal-user role claims transformation, every policy, and the
/// <see cref="InternalOrExternalAuthenticationScheme"/> that is the app's default authentication scheme. One call from
/// <c>Program.cs</c> - see <c>src/CDC.Web/Program.cs</c> for pipeline registration
/// (<c>app.UseAuthentication()</c> before <c>app.UseAuthorization()</c>).
/// </summary>
public static class AuthorizationDependencyInjection
{
    /// <summary>Adds the authorization framework to the container.</summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddCdcWebAuthorization(this IServiceCollection services)
    {
        services.AddScoped<INavigationAuthorizationService, NavigationAuthorizationService>();
        services.AddSingleton<IAuthorizationHandler, Handlers.AllowedRolesAuthorizationHandler>();
        services.AddTransient<IClaimsTransformation, InternalRoleClaimsTransformation>();

        services.AddAuthentication().AddPolicyScheme(
            InternalOrExternalAuthenticationScheme.Name,
            InternalOrExternalAuthenticationScheme.Name,
            options => options.ForwardDefaultSelector = InternalOrExternalAuthenticationScheme.SelectCookieScheme);

        // Post-configured so it wins whatever order the identity providers register in; each of them sets a default scheme of its own.
        services.PostConfigure<AuthenticationOptions>(options => options.DefaultScheme = InternalOrExternalAuthenticationScheme.Name);

        services.AddAuthorizationBuilder();
        services.Configure<AuthorizationOptions>(options => options.AddCdcWebPolicies());

        return services;
    }
}
