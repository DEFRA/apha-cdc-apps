using System.Globalization;
using System.Security.Claims;
using CDC.Auth.Cidm;
using CDC.Auth.Entra;
using CDC.Web.Features.LoadTesting;
using CDC.Web.Infrastructure;
using CDC.Web.Tests.Integration;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
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
            ["LoadTesting:Enabled"] = enabled.ToString()
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

    private static ServiceProvider Build(
        Dictionary<string, string?> settings,
        ControllableClock? clock = null,
        string hostEnvironment = "Development",
        ILoggerProvider? loggerProvider = null)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            if (loggerProvider is not null)
            {
                logging.AddProvider(loggerProvider);
            }
        });
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new FakeHostEnvironment(hostEnvironment));
        services.AddSingleton<TimeProvider>(clock ?? new ControllableClock(Now));
        services.AddSingleton<IApiClient>(new FakeApiClient());
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void WhenTheTaskDefinitionPassesABlankValue_ItIsOff(string blank)
    {
        using var services = Build(new Dictionary<string, string?> { ["LoadTesting:Enabled"] = blank });

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
    public void WhenOn_TheExpiryIsBound()
    {
        using var services = Build(Settings(enabled: true, expiresAtUtc: "2026-11-05T12:00:00Z"));

        var options = services.GetRequiredService<IOptions<LoadTestingOptions>>().Value;

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

    [Theory]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData(" true ", true)]
    public void TheSwitchIsOnOnlyWhenTheTaskDefinitionGivesItAValueOfTrue(string value, bool expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["LoadTesting:Enabled"] = value }).Build();

        Assert.Equal(expected, LoadTestingOptions.IsEnabledIn(configuration));
    }

    [Fact]
    public void AValueThatIsNotABooleanStillStopsTheApp()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["LoadTesting:Enabled"] = "maybe" }).Build();

        Assert.Throws<InvalidOperationException>(() => LoadTestingOptions.IsEnabledIn(configuration));
    }

    [Fact]
    public void WhenOnWithABlankExpiry_NoExpiryIsBound()
    {
        using var services = Build(new Dictionary<string, string?> { ["LoadTesting:Enabled"] = "true", ["LoadTesting:ExpiresAtUtc"] = "" });

        var options = services.GetRequiredService<IOptions<LoadTestingOptions>>().Value;

        Assert.True(options.Enabled);
        Assert.Null(options.ExpiresAtUtc);
    }

    private static IConfiguration WithCutoff(string? cutoffDate, string enabled = "true") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LoadTesting:Enabled"] = enabled,
            [LoadTestingOptions.CutoffDateKey] = cutoffDate
        }).Build();

    // A date allows the whole of that day (UTC); a date and time is the exact moment, its offset honoured.
    [Theory]
    [InlineData("2026-11-04", "2026-11-05T00:00:00Z")]
    [InlineData(" 2026-12-31 ", "2027-01-01T00:00:00Z")]
    [InlineData("2026-11-04T18:30:00Z", "2026-11-04T18:30:00Z")]
    [InlineData("2026-11-04T18:30:00+01:00", "2026-11-04T17:30:00Z")]
    [InlineData("2026-11-04T18:30", "2026-11-04T18:30:00Z")]
    [InlineData("2026-11-04T18:30:00.5Z", "2026-11-04T18:30:00.5Z")]
    public void TheCutoffDate_IsReadAsAnIso8601DateOrDateAndTime(string value, string expected)
    {
        Assert.True(LoadTestingOptions.TryReadCutoff(WithCutoff(value), out var cutoff));
        Assert.Equal(DateTimeOffset.Parse(expected, CultureInfo.InvariantCulture), cutoff);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void WithNoCutoffDate_ThereIsNoCutoff(string? value)
    {
        Assert.True(LoadTestingOptions.TryReadCutoff(WithCutoff(value), out var cutoff));
        Assert.Null(cutoff);
        Assert.True(LoadTestingOptions.IsEnabledIn(WithCutoff(value), new ControllableClock(Now)));
    }

    // A value that is not an ISO 8601 date keeps LoadTesting off, so a typo cannot leave the bypass on with no hard stop.
    [Theory]
    [InlineData("31/12/2099")]
    [InlineData("12/31/2099")]
    [InlineData("soon")]
    [InlineData("2099-13-01")]
    public void ACutoffDateThatIsNotADate_KeepsItOff(string value)
    {
        Assert.False(LoadTestingOptions.TryReadCutoff(WithCutoff(value), out _));
        Assert.False(LoadTestingOptions.IsEnabledIn(WithCutoff(value), new ControllableClock(Now)));
    }

    [Theory]
    [InlineData("2026-11-04", true)]
    [InlineData("2026-11-03", false)]
    [InlineData("2026-11-04T12:00:01Z", true)]
    [InlineData("2026-11-04T12:00:00Z", false)]
    public void TheSwitchIsOn_OnlyUntilTheCutoff(string cutoffDate, bool expected)
    {
        Assert.Equal(expected, LoadTestingOptions.IsEnabledIn(WithCutoff(cutoffDate), new ControllableClock(Now)));
    }

    [Fact]
    public void ACutoffDate_DoesNotSwitchItOn()
    {
        Assert.False(LoadTestingOptions.IsEnabledIn(WithCutoff("2099-12-31", enabled: "false"), new ControllableClock(Now)));
    }

    // Past the cutoff nothing is registered, and even a deployment the switch is refused in starts, because it is off.
    [Fact]
    public void WhenThePassedCutoffHoldsItOff_NothingIsRegisteredAndTheOptionsAreNotRefused()
    {
        using var services = Build(new Dictionary<string, string?>
        {
            ["LoadTesting:Enabled"] = "true",
            [LoadTestingOptions.CutoffDateKey] = "2020-01-01",
            [LoadTestingOptionsValidator.DeploymentKey] = "PROD"
        });

        var options = services.GetRequiredService<IOptions<LoadTestingOptions>>().Value;

        Assert.False(options.Enabled);
        Assert.False(services.GetRequiredService<ILoadTestingStatus>().IsActive);
        Assert.Null(services.GetService<ILoadTestSignIn>());
        Assert.Equal(EntraAuthenticationDefaults.AuthenticationScheme, ForwardChallenge(services, EntraAuthenticationDefaults.CookieAuthenticationScheme));
    }

    [Fact]
    public void WhenOnWithAFutureCutoff_TheCutoffIsBound()
    {
        using var services = Build(new Dictionary<string, string?> { ["LoadTesting:Enabled"] = "true", [LoadTestingOptions.CutoffDateKey] = "2099-12-31" });

        var status = services.GetRequiredService<ILoadTestingStatus>();

        Assert.True(status.IsActive);
        Assert.Equal(new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero), status.CutoffUtc);
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

    // An ECS task runs as Production and may carry no deployment label; the warning then names the hosting environment, not "local".
    [Theory]
    [InlineData(null, "Production")]
    [InlineData("TEST", "TEST")]
    public void UseLoadTesting_WhenOn_WarnsWhereItIsRunning(string? deployment, string expected)
    {
        var logs = new CapturingLoggerProvider();
        using var services = Build(Settings(enabled: true, expiresAtUtc: "2026-11-05T12:00:00Z", deployment: deployment), hostEnvironment: "Production", loggerProvider: logs);

        new ApplicationBuilder(services).UseLoadTesting();

        var warning = Assert.Single(logs.Messages, message => message.Contains("LoadTesting is ENABLED", StringComparison.Ordinal));
        Assert.Contains($"(deployment {expected},", warning, StringComparison.Ordinal);
    }

    [Fact]
    public void UseLoadTesting_WhenTheCutoffHoldsItOff_WarnsThatItStaysOff()
    {
        var logs = new CapturingLoggerProvider();
        using var services = Build(
            new Dictionary<string, string?> { ["LoadTesting:Enabled"] = "true", [LoadTestingOptions.CutoffDateKey] = "2020-01-01" },
            loggerProvider: logs);

        new ApplicationBuilder(services).UseLoadTesting();

        Assert.Contains(logs.Messages, message => message.Contains("stays OFF: its cutoff date '2020-01-01'", StringComparison.Ordinal));
        Assert.DoesNotContain(logs.Messages, message => message.Contains("LoadTesting is ENABLED", StringComparison.Ordinal));
    }

    [Fact]
    public void UseLoadTesting_WhenSwitchedOff_SaysNothingAboutTheCutoff()
    {
        var logs = new CapturingLoggerProvider();
        using var services = Build(
            new Dictionary<string, string?> { ["LoadTesting:Enabled"] = "false", [LoadTestingOptions.CutoffDateKey] = "2020-01-01" },
            loggerProvider: logs);

        new ApplicationBuilder(services).UseLoadTesting();

        Assert.DoesNotContain(logs.Messages, message => message.Contains("LoadTesting", StringComparison.Ordinal));
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider, Microsoft.Extensions.Logging.ILogger
    {
        public List<string> Messages { get; } = [];

        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) => this;

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));

        public void Dispose()
        {
        }
    }

    [Fact]
    public void LogEvents_AreTaggedWithLoadTesting_OnlyWhileItIsOn()
    {
        var on = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["LoadTesting:Enabled"] = "true" }).Build();
        var off = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["LoadTesting:Enabled"] = "false" }).Build();

        Assert.True(LoggedProperties(on).TryGetValue("LoadTesting", out var tagged));
        Assert.True(Assert.IsType<ScalarValue>(tagged).Value is true);
        Assert.False(LoggedProperties(off).ContainsKey("LoadTesting"));
        Assert.False(LoggedProperties(WithCutoff("2020-01-01")).ContainsKey("LoadTesting"));
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
