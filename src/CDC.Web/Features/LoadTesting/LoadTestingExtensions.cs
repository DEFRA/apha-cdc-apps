using CDC.Auth.Entra;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Serilog;

namespace CDC.Web.Features.LoadTesting;

/// <summary>Registers LoadTesting: a switch that makes signing in as an internal or external user skip the identity provider.</summary>
public static class LoadTestingExtensions
{
    /// <summary>
    /// Reads and validates the <c>LoadTesting</c> section; a missing or blank <c>Enabled</c> means off, and so does a passed
    /// <c>CutoffDate</c>. Only while it is switched on does this register the sign-in bypass; with it off, the app is exactly as it is
    /// without LoadTesting.
    /// </summary>
    /// <param name="services">The service collection; the identity providers' cookie schemes must be registered on it.</param>
    /// <param name="configuration">The configuration holding the <c>LoadTesting</c> section.</param>
    /// <param name="timeProvider">The clock the cutoff is checked against at startup; the system clock when not given.</param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddLoadTesting(this IServiceCollection services, IConfiguration configuration, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var enabled = LoadTestingOptions.IsEnabledIn(configuration, timeProvider);
        LoadTestingOptions.TryReadCutoff(configuration, out var cutoffUtc);

        services.AddOptions<LoadTestingOptions>()
            .Configure(options =>
            {
                options.Enabled = enabled;
                options.ExpiresAtUtc = configuration.GetValue<DateTimeOffset?>($"{LoadTestingOptions.SectionName}:{nameof(LoadTestingOptions.ExpiresAtUtc)}");
                options.CutoffUtc = cutoffUtc;
            })
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<LoadTestingOptions>, LoadTestingOptionsValidator>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ILoadTestingStatus, LoadTestingStatus>();

        if (!enabled)
        {
            return services;
        }

        services.AddScoped<ILoadTestSignIn, LoadTestSignIn>();

        // A page that needs a user sends an anonymous request to Account/Login or Account/LoginInternal, which send the user to the Load
        // test login page to say who to sign in as, instead of straight to the identity provider.
        foreach (var cookieScheme in new[] { CookieAuthenticationDefaults.AuthenticationScheme, EntraAuthenticationDefaults.CookieAuthenticationScheme })
        {
            services.PostConfigure<CookieAuthenticationOptions>(cookieScheme, cookie => cookie.ForwardChallenge = null);
        }

        return services;
    }

    /// <summary>Adds the middleware that ends a LoadTesting session after the window has passed, and warns in the log that LoadTesting is on.</summary>
    /// <param name="app">The application pipeline; must come after authentication.</param>
    /// <returns>The same pipeline, for chaining.</returns>
    public static IApplicationBuilder UseLoadTesting(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var status = app.ApplicationServices.GetRequiredService<ILoadTestingStatus>();
        var configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();
        if (!status.IsEnabled)
        {
            // Switched on, but held off by the cutoff: say so, as the operator will expect it to be on.
            if (LoadTestingOptions.IsSwitchedOnIn(configuration))
            {
                app.ApplicationServices.GetRequiredService<ILogger<LoadTestingSessionMiddleware>>()
                    .HeldOffByCutoff(configuration[LoadTestingOptions.CutoffDateKey] ?? string.Empty);
            }

            return app;
        }

        var deployment = configuration[LoadTestingOptionsValidator.DeploymentKey];
        var hostEnvironment = app.ApplicationServices.GetRequiredService<IHostEnvironment>().EnvironmentName;
        app.ApplicationServices.GetRequiredService<ILogger<LoadTestingSessionMiddleware>>()
            .Enabled(string.IsNullOrEmpty(deployment) ? hostEnvironment : deployment, status.ExpiresAtUtc, status.CutoffUtc);

        return app.UseMiddleware<LoadTestingSessionMiddleware>();
    }

    /// <summary>Tags every log event with <c>LoadTesting=true</c> while LoadTesting is switched on, so its traffic is easy to find and alarm on.</summary>
    /// <param name="configuration">The logger configuration.</param>
    /// <param name="settings">The application configuration.</param>
    /// <returns>The same logger configuration, for chaining.</returns>
    public static LoggerConfiguration EnrichWhenLoadTesting(this LoggerConfiguration configuration, IConfiguration settings)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(settings);

        return LoadTestingOptions.IsEnabledIn(settings) ? configuration.Enrich.WithProperty("LoadTesting", true) : configuration;
    }
}
