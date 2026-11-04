using CDC.Web.Features.LoadTesting;
using CDC.Web.Tests.TestSupport;
using Microsoft.Extensions.Options;

namespace CDC.Web.Tests.Features.LoadTesting;

public class LoadTestingStatusTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 4, 12, 0, 0, TimeSpan.Zero);

    private static LoadTestingStatus StatusOf(LoadTestingOptions options, ControllableClock clock) => new(Options.Create(options), clock);

    [Fact]
    public void WhenOff_NothingIsEnabledOrActive()
    {
        var status = StatusOf(new LoadTestingOptions(), new ControllableClock(Now));

        Assert.False(status.IsEnabled);
        Assert.False(status.IsActive);
    }

    [Fact]
    public void WhenOnWithNoExpiry_ItIsActive()
    {
        var status = StatusOf(new LoadTestingOptions { Enabled = true }, new ControllableClock(Now));

        Assert.True(status.IsEnabled);
        Assert.True(status.IsActive);
        Assert.Null(status.ExpiresAtUtc);
    }

    [Fact]
    public void WhenOnWithAnExpiry_ItIsActiveUntilThatMoment()
    {
        var clock = new ControllableClock(Now);
        var status = StatusOf(new LoadTestingOptions { Enabled = true, ExpiresAtUtc = Now.AddHours(1) }, clock);

        Assert.True(status.IsActive);
        Assert.Equal(Now.AddHours(1), status.ExpiresAtUtc);

        clock.Advance(TimeSpan.FromHours(1).Subtract(TimeSpan.FromSeconds(1)));
        Assert.True(status.IsActive);

        clock.Advance(TimeSpan.FromSeconds(1));
        Assert.False(status.IsActive);
        Assert.True(status.IsEnabled);
    }
}
