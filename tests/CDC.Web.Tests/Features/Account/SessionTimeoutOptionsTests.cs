using CDC.Auth.Entra;
using CDC.Web.Features.Account;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CDC.Web.Tests.Features.Account;

public class SessionTimeoutOptionsTests
{
    private static ServiceProvider BuildServices(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSessionTimeout(configuration);

        return services.BuildServiceProvider();
    }

    private static TimeSpan CookieLifetime(ServiceProvider services, string cookieScheme) =>
        services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(cookieScheme).ExpireTimeSpan;

    [Fact]
    public void WithNoConfiguration_TheTimeoutsAreThoseOfTheLegacySession()
    {
        using var services = BuildServices([]);

        var options = services.GetRequiredService<IOptions<SessionTimeoutOptions>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(20), options.IdleTimeout);
        Assert.Equal(TimeSpan.FromMinutes(5), options.WarningBeforeExpiry);
    }

    [Fact]
    public void TheIdleTimeoutIsTheLifetimeOfBothSignInCookies()
    {
        using var services = BuildServices(new()
        {
            ["SessionTimeout:IdleTimeout"] = "00:45:00",
            ["SessionTimeout:WarningBeforeExpiry"] = "00:02:00"
        });

        var options = services.GetRequiredService<IOptions<SessionTimeoutOptions>>().Value;

        Assert.Equal(TimeSpan.FromMinutes(45), options.IdleTimeout);
        Assert.Equal(TimeSpan.FromMinutes(2), options.WarningBeforeExpiry);
        Assert.Equal(TimeSpan.FromMinutes(45), CookieLifetime(services, CookieAuthenticationDefaults.AuthenticationScheme));
        Assert.Equal(TimeSpan.FromMinutes(45), CookieLifetime(services, EntraAuthenticationDefaults.CookieAuthenticationScheme));
    }

    [Fact]
    public void AnInvalidConfigurationIsRefusedRatherThanAppliedToASession()
    {
        using var services = BuildServices(new() { ["SessionTimeout:WarningBeforeExpiry"] = "00:00:05" });

        var failure = Assert.Throws<OptionsValidationException>(() => services.GetRequiredService<IOptions<SessionTimeoutOptions>>().Value);

        Assert.Contains("WarningBeforeExpiry", failure.Message);
    }

    [Fact]
    public void TheDefaultsAreValid()
    {
        var result = new SessionTimeoutOptionsValidator().Validate(null, new SessionTimeoutOptions());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(20, 1200)]
    [InlineData(300, 301)]
    public void TheShortestWarningAndTheShortestIdleTimeoutAboveItAreValid(int warningSeconds, int idleSeconds)
    {
        var options = new SessionTimeoutOptions { WarningBeforeExpiry = TimeSpan.FromSeconds(warningSeconds), IdleTimeout = TimeSpan.FromSeconds(idleSeconds) };

        Assert.True(new SessionTimeoutOptionsValidator().Validate(null, options).Succeeded);
    }

    [Fact]
    public void AWarningTooShortToRespondToIsRefused()
    {
        var options = new SessionTimeoutOptions { WarningBeforeExpiry = TimeSpan.FromSeconds(19) };

        var result = new SessionTimeoutOptionsValidator().Validate(null, options);

        var failure = Assert.Single(result.Failures!);
        Assert.Contains("WarningBeforeExpiry must be at least 20 seconds", failure);
    }

    [Theory]
    [InlineData(300, 300)]
    [InlineData(300, 240)]
    public void AnIdleTimeoutNoLongerThanTheWarningIsRefused(int warningSeconds, int idleSeconds)
    {
        var options = new SessionTimeoutOptions { WarningBeforeExpiry = TimeSpan.FromSeconds(warningSeconds), IdleTimeout = TimeSpan.FromSeconds(idleSeconds) };

        var result = new SessionTimeoutOptionsValidator().Validate(null, options);

        var failure = Assert.Single(result.Failures!);
        Assert.Contains("IdleTimeout must be longer than WarningBeforeExpiry", failure);
    }

    [Fact]
    public void EveryProblemIsReportedAtOnce()
    {
        var options = new SessionTimeoutOptions { WarningBeforeExpiry = TimeSpan.FromSeconds(5), IdleTimeout = TimeSpan.FromSeconds(5) };

        var result = new SessionTimeoutOptionsValidator().Validate(null, options);

        Assert.Equal(2, result.Failures!.Count());
    }
}
