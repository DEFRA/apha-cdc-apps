using System.Security.Claims;
using CDC.Auth.Cidm;
using CDC.Auth.Cidm.Events;
using CDC.Auth.Entra;
using CDC.Auth.Entra.Events;
using CDC.Web.Features.LoadTesting;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace CDC.Web.Tests.Features.LoadTesting;

public class LoadTestingExtensionsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static Dictionary<string, string?> Settings(bool enabled, string? expiresAtUtc = null, string? deployment = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["LoadTesting:Enabled"] = enabled.ToString(),
            ["LoadTesting:InternalUserName"] = "internal.user",
            ["LoadTesting:InternalSsoUserId"] = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
            ["LoadTesting:ExternalUserEmail"] = "external.user@example.test",
            ["LoadTesting:ExternalSsoUserId"] = "bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"
        };

        if (expiresAtUtc is not null)
        {
            settings["LoadTesting:ExpiresAtUtc"] = expiresAtUtc;
        }

        if (deployment is not null)
        {
            settings[LoadTestingOptionsValidator.DeploymentKey] = deployment;
        }

        return settings;
    }

    private static ServiceProvider Build(Dictionary<string, string?> settings, ControllableClock? clock = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var resolvers = new FakeIdentityResolvers();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment("Development"));
        services.AddSingleton<TimeProvider>(clock ?? new ControllableClock(Now));
        services.AddSingleton<IEntraInternalUserResolver>(resolvers);
        services.AddSingleton<ICidmExternalUserResolver>(resolvers);
        services.AddAuthentication()
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options => options.ForwardChallenge = CidmAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(EntraAuthenticationDefaults.CookieAuthenticationScheme, options => options.ForwardChallenge = EntraAuthenticationDefaults.AuthenticationScheme);
        services.AddLoadTesting(configuration);

        return services.BuildServiceProvider();
    }

    private static string? ForwardChallenge(ServiceProvider services, string scheme) =>
        services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(scheme).ForwardChallenge;

    [Fact]
    public void WhenOff_OnlyTheStatusIsRegisteredAndTheCookiesStillChallengeTheIdentityProviders()
    {
        using var services = Build(Settings(enabled: false));

        Assert.False(services.GetRequiredService<ILoadTestingStatus>().IsEnabled);
        Assert.False(services.GetRequiredService<ILoadTestingStatus>().IsActive);
        Assert.Null(services.GetService<ILoadTestSignIn>());
        Assert.Equal(CidmAuthenticationDefaults.AuthenticationScheme, ForwardChallenge(services, CookieAuthenticationDefaults.AuthenticationScheme));
        Assert.Equal(EntraAuthenticationDefaults.AuthenticationScheme, ForwardChallenge(services, EntraAuthenticationDefaults.CookieAuthenticationScheme));
    }

    [Fact]
    public void WhenNothingIsConfigured_ItIsOff()
    {
        using var services = Build([]);

        Assert.False(services.GetRequiredService<ILoadTestingStatus>().IsEnabled);
        Assert.Null(services.GetService<ILoadTestSignIn>());
    }

    [Fact]
    public void WhenOn_TheSignInIsRegisteredAndAnAnonymousRequestIsSentToTheAppsOwnLoginPathNotTheIdentityProvider()
    {
        using var services = Build(Settings(enabled: true));

        Assert.True(services.GetRequiredService<ILoadTestingStatus>().IsActive);
        Assert.NotNull(services.GetRequiredService<ILoadTestSignIn>());
        Assert.Null(ForwardChallenge(services, CookieAuthenticationDefaults.AuthenticationScheme));
        Assert.Null(ForwardChallenge(services, EntraAuthenticationDefaults.CookieAuthenticationScheme));
    }

    [Fact]
    public void WhenOn_TheConfiguredUsersAreBound()
    {
        using var services = Build(Settings(enabled: true, expiresAtUtc: "2026-11-05T12:00:00Z"));

        var options = services.GetRequiredService<IOptions<LoadTestingOptions>>().Value;

        Assert.Equal("internal.user", options.InternalUserName);
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), options.InternalSsoUserId);
        Assert.Equal("external.user@example.test", options.ExternalUserEmail);
        Assert.Equal(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), options.ExternalSsoUserId);
        Assert.Equal(new DateTimeOffset(2026, 11, 5, 12, 0, 0, TimeSpan.Zero), options.ExpiresAtUtc);
    }

    [Fact]
    public void WhenOnInProduction_TheOptionsAreRefused()
    {
        using var services = Build(Settings(enabled: true, expiresAtUtc: "2026-11-05T12:00:00Z", deployment: "PROD"));

        var failure = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<LoadTestingOptions>>().Value);

        Assert.Contains("'PROD'", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheEnabledSwitchIsReadWithoutBindingTheRestOfTheSection()
    {
        var on = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["LoadTesting:Enabled"] = "true" }).Build();
        var off = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["LoadTesting:Enabled"] = "false" }).Build();

        Assert.True(LoadTestingOptions.IsEnabledIn(on));
        Assert.False(LoadTestingOptions.IsEnabledIn(off));
        Assert.False(LoadTestingOptions.IsEnabledIn(new ConfigurationBuilder().Build()));
    }

    // A request carrying a LoadTesting session after the window has passed: ended when LoadTesting is on, untouched when it is off.
    private static async Task<int> RequestWithAnExpiredSessionAsync(ServiceProvider services)
    {
        var app = new ApplicationBuilder(services).UseLoadTesting();
        var pipeline = app.Build();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("loadTestSession", bool.TrueString)], "test")),
            RequestServices = new ServiceCollection().AddSingleton<IAuthenticationService>(new FakeAuthenticationService()).BuildServiceProvider()
        };

        await pipeline(context);

        return context.Response.StatusCode;
    }

    [Fact]
    public async Task UseLoadTesting_WhenOn_EndsASessionOnceTheWindowHasPassed()
    {
        using var services = Build(Settings(enabled: true, expiresAtUtc: "2026-11-04T11:00:00Z", deployment: "TEST"));

        Assert.Equal(StatusCodes.Status302Found, await RequestWithAnExpiredSessionAsync(services));
    }

    [Fact]
    public async Task UseLoadTesting_WhenOff_AddsNothingToThePipeline()
    {
        using var services = Build(Settings(enabled: false));

        Assert.Equal(StatusCodes.Status404NotFound, await RequestWithAnExpiredSessionAsync(services));
    }

    [Fact]
    public void LogEvents_AreTaggedWithLoadTesting_OnlyWhileItIsOn()
    {
        var on = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["LoadTesting:Enabled"] = "true" }).Build();
        var off = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["LoadTesting:Enabled"] = "false" }).Build();

        Assert.True(LoggedProperties(on).TryGetValue("LoadTesting", out var tagged));
        Assert.True(Assert.IsType<ScalarValue>(tagged).Value is true);
        Assert.False(LoggedProperties(off).ContainsKey("LoadTesting"));
    }

    private static IReadOnlyDictionary<string, LogEventPropertyValue> LoggedProperties(IConfiguration settings)
    {
        var sink = new CollectingSink();
        using var logger = new LoggerConfiguration().EnrichWhenLoadTesting(settings).WriteTo.Sink(sink).CreateLogger();

        logger.Information("An event");

        return Assert.Single(sink.Events).Properties;
    }

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
