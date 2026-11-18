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

    private static ValidateOptionsResult Validate(LoadTestingOptions options, string? deployment = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(deployment is null ? [] : new Dictionary<string, string?> { [LoadTestingOptionsValidator.DeploymentKey] = deployment })
            .Build();

        return new LoadTestingOptionsValidator(configuration, new ControllableClock(Now)).Validate(null, options);
    }

    [Fact]
    public void WhenOff_NothingIsChecked()
    {
        var result = Validate(new LoadTestingOptions(), "PROD");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void WithNoDeploymentConfigured_NeitherADeploymentNorAnExpiryIsNeeded()
    {
        var result = Validate(On());

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData("DEV")]
    [InlineData("test")]
    [InlineData("Pre-Prod")]
    [InlineData(" TEST ")]
    public void InAnAllowedDeployment_WithinTheWindow_IsAccepted(string deployment)
    {
        var result = Validate(On(Now.AddDays(1)), deployment);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void InProduction_IsRefused()
    {
        var result = Validate(On(Now.AddDays(1)), "PROD");

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("'PROD'", StringComparison.Ordinal));
    }

    [Fact]
    public void InAnUnknownDeployment_IsRefused()
    {
        var result = Validate(On(Now.AddDays(1)), "UAT");

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("'UAT'", StringComparison.Ordinal));
    }

    [Fact]
    public void AWhitespaceDeployment_IsTreatedAsNone()
    {
        var result = Validate(On(), "  ");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void InADeployedEnvironment_AnExpiryIsRequired()
    {
        var result = Validate(On(), "TEST");

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("ExpiresAtUtc is required", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("TEST")]
    [InlineData(null)]
    public void AnExpiryMoreThanAWeekAway_IsRefused(string? deployment)
    {
        var result = Validate(On(Now.AddDays(7).AddMinutes(1)), deployment);

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("7 days", StringComparison.Ordinal));
    }

    [Fact]
    public void AnExpiryExactlyAWeekAway_IsAccepted()
    {
        var result = Validate(On(Now.AddDays(7)), "TEST");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void AnExpiryThatHasPassed_IsAcceptedAndSimplyLeavesTheBypassOff()
    {
        var result = Validate(On(Now.AddHours(-1)), "TEST");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void NoUserToSignInAsIsConfigured_BecauseTheUserIsChosenOnTheLoginPage()
    {
        var result = Validate(new LoadTestingOptions { Enabled = true });

        Assert.True(result.Succeeded);
    }
}
