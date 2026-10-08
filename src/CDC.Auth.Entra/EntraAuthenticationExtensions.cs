using CDC.Auth.Entra.Events;
using CDC.Auth.Entra.Options;
using CDC.Auth.Entra.TokenRefresh;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;

namespace CDC.Auth.Entra;

/// <summary>Registers Microsoft Entra ID (OpenID Connect) sign-in for internal users.</summary>
public static class EntraAuthenticationExtensions
{
    /// <summary>
    /// Adds a dedicated Cookie + <see cref="EntraAuthenticationDefaults.AuthenticationScheme"/> OpenID
    /// Connect authentication pair, bound to and validated against the <c>Entra</c> configuration
    /// section. Registers its own cookie scheme (<see cref="EntraAuthenticationDefaults.CookieAuthenticationScheme"/>)
    /// rather than reusing the default one, so it can coexist with another identity provider (e.g.
    /// CIDM for external users) registered elsewhere in the same app.
    /// </summary>
    /// <param name="builder">The host builder to register authentication services on.</param>
    /// <returns>The same builder, for chaining.</returns>
    /// <remarks>
    /// Call this AFTER any other <c>Add*Authentication</c> extension that also calls
    /// <c>AddAuthentication(defaultScheme)</c> (e.g. <c>AddCidmAuthentication</c>) - whichever is
    /// registered last wins the app-wide default scheme, and this app is predominantly used by
    /// internal (Entra-authenticated) staff, so Entra's cookie should be the fallback.
    /// </remarks>
    public static WebApplicationBuilder AddEntraAuthentication(this WebApplicationBuilder builder)
    {
        // ClientId/ClientSecret/TenantId arrive as plain configuration - the ECS task definition's
        // "secrets" block resolves Parameter Store/Secrets Manager values into Entra__ClientId,
        // Entra__ClientSecret, Entra__TenantId environment variables before the container starts, and
        // ASP.NET Core's built-in environment variable configuration provider maps double-underscore
        // names to the "Entra:*" section automatically. Locally, the same "Entra:*" keys come from
        // dotnet user-secrets / appsettings.Development.json instead.
        builder.Services.AddOptions<EntraOptions>()
            .Bind(builder.Configuration.GetSection(EntraOptions.SectionName))
            .ValidateOnStart();
        builder.Services.AddSingleton<IValidateOptions<EntraOptions>, EntraOptionsValidator>();

        builder.Services.AddHttpClient(EntraTokenRefreshService.HttpClientName)
            .AddStandardResilienceHandler();
        builder.Services.AddSingleton<IEntraTokenRefreshService, EntraTokenRefreshService>();
        // TryAdd - AddCidmAuthentication may have already registered the same static TimeProvider.System
        // instance; re-adding it outright would just register the identical singleton a second time.
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<EntraOpenIdConnectEvents>();
        builder.Services.AddSingleton<EntraCookieEvents>();

        var useLocalHttpFriendlyOidcSettings = builder.Environment.IsDevelopment();

        builder.Services.AddAuthentication(EntraAuthenticationDefaults.CookieAuthenticationScheme)
            .AddCookie(EntraAuthenticationDefaults.CookieAuthenticationScheme, options =>
            {
                options.LoginPath = "/Account/LoginInternal";
                options.LogoutPath = "/Account/LogoutInternal";
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = useLocalHttpFriendlyOidcSettings
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                // Any [Authorize] using this scheme redirects straight to the Entra challenge instead
                // of returning a bare 401.
                options.ForwardChallenge = EntraAuthenticationDefaults.AuthenticationScheme;
                options.EventsType = typeof(EntraCookieEvents);
            })
            .AddOpenIdConnect(EntraAuthenticationDefaults.AuthenticationScheme, _ => { });

        // Configured via DI (rather than inline in AddOpenIdConnect above) so EntraOptions is resolved
        // through the validated IOptions<EntraOptions> pipeline instead of being parsed a second time.
        builder.Services.AddOptions<OpenIdConnectOptions>(EntraAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<EntraOptions>>((options, entraOptions) =>
            {
                var entra = entraOptions.Value;

                // Pinned explicitly rather than left to resolve from the app's ambient default
                // scheme at runtime - see the matching comment in CidmAuthenticationExtensions for
                // why that's not safe once more than one Add*Authentication call exists in the app.
                options.SignInScheme = EntraAuthenticationDefaults.CookieAuthenticationScheme;
                options.Authority = entra.Authority;
                options.ClientId = entra.ClientId;
                options.ClientSecret = entra.ClientSecret;
                options.ResponseType = entra.ResponseType;
                options.ResponseMode = useLocalHttpFriendlyOidcSettings ? "query" : entra.ResponseMode;
                options.CallbackPath = entra.CallbackPath;
                options.SignedOutCallbackPath = entra.SignedOutCallbackPath;
                // Unlike CIDM (which only ever uses the redirect-based sign-out flow and nulls this
                // out), Entra ID's front-channel logout notifications are a real scenario for a
                // predominantly-internal, single-sign-on app - this path must be registered as the
                // app's "Front-channel logout URL" in the Entra ID app registration.
                options.RemoteSignOutPath = entra.FrontChannelLogoutPath;
                options.SaveTokens = true;
                options.UsePkce = true;
                options.GetClaimsFromUserInfoEndpoint = false;

                options.Scope.Clear();
                foreach (var scope in entra.Scopes)
                {
                    options.Scope.Add(scope);
                }

                if (useLocalHttpFriendlyOidcSettings)
                {
                    options.CorrelationCookie.SameSite = SameSiteMode.Lax;
                    options.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    options.NonceCookie.SameSite = SameSiteMode.Lax;
                    options.NonceCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                }

                options.EventsType = typeof(EntraOpenIdConnectEvents);
            });

        return builder;
    }
}
