using CDC.Web.Features.LoadTesting;
using CDC.Web.Tests.TestSupport;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace CDC.Web.Tests.Features.LoadTesting;

public class LoadTestingOptionsValidatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static LoadTestingOptions On(DateTimeOffset? expiresAtUtc = null) => new()
    {
        Enabled = true,
        ExpiresAtUtc = expiresAtUtc
    };

    private static ValidateOptionsResult Validate(LoadTestingOptions options, string environment, string? deployment = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(deployment is null ? [] : new Dictionary<string, string?> { [LoadTestingOptionsValidator.DeploymentKey] = deployment })
            .Build();

        return new LoadTestingOptionsValidator(configuration, new FakeHostEnvironment(environment), new ControllableClock(Now)).Validate(null, options);
    }

    [Fact]
    public void WhenOff_NothingIsChecked()
    {
        var result = Validate(new LoadTestingOptions(), "Production", "PROD");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void OnALocalDevelopmentRun_NeitherADeploymentNorAnExpiryIsNeeded()
    {
        var result = Validate(On(), "Development");

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("DEV")]
    [InlineData("test")]
    [InlineData("Pre-Prod")]
    [InlineData(" TEST ")]
    public void InAnAllowedDeployment_WithinTheWindow_IsAccepted(string deployment)
    {
        var result = Validate(On(Now.AddDays(1)), "Production", deployment);

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void InProduction_IsRefusedWhateverTheHostingEnvironment(string environment)
    {
        var result = Validate(On(Now.AddDays(1)), environment, "PROD");

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("'PROD'", StringComparison.Ordinal));
    }

    [Fact]
    public void InAnUnknownDeployment_IsRefused()
    {
        var result = Validate(On(Now.AddDays(1)), "Production", "UAT");

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("'UAT'", StringComparison.Ordinal));
    }

    [Fact]
    public void WithNoDeploymentOutsideALocalRun_IsRefused()
    {
        var result = Validate(On(Now.AddDays(1)), "Production");

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains(LoadTestingOptionsValidator.DeploymentKey, StringComparison.Ordinal));
    }

    [Fact]
    public void AWhitespaceDeployment_IsTreatedAsNone()
    {
        var result = Validate(On(), "Development", "  ");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void InADeployedEnvironment_AnExpiryIsRequired()
    {
        var result = Validate(On(), "Production", "TEST");

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("ExpiresAtUtc is required", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Production", "TEST")]
    [InlineData("Development", null)]
    public void AnExpiryMoreThanAWeekAway_IsRefused(string environment, string? deployment)
    {
        var result = Validate(On(Now.AddDays(7).AddMinutes(1)), environment, deployment);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("7 days", StringComparison.Ordinal));
    }

    [Fact]
    public void AnExpiryExactlyAWeekAway_IsAccepted()
    {
        var result = Validate(On(Now.AddDays(7)), "Production", "TEST");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void AnExpiryThatHasPassed_IsAcceptedAndSimplyLeavesTheBypassOff()
    {
        var result = Validate(On(Now.AddHours(-1)), "Production", "TEST");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void NoUserToSignInAsIsConfigured_BecauseTheUserIsChosenOnTheLoginPage()
    {
        var result = Validate(new LoadTestingOptions { Enabled = true }, "Development");

        Assert.True(result.Succeeded);
    }
}
