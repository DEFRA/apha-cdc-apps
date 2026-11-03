using CDC.Graph.Authentication;
using CDC.Graph.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CDC.Graph.Tests;

public class GraphRegistrationTests
{
    private static ServiceProvider Build(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddGraphClient(configuration);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void Disabled_RegistersTheNoOpClient_AndNeedsNoCredentials()
    {
        using var provider = Build(new() { ["Graph:Enabled"] = "false" });

        Assert.IsType<DisabledGraphClient>(provider.GetRequiredService<IGraphClient>());
        Assert.Null(provider.GetService<IGraphTokenProvider>());
        Assert.NotNull(provider.GetRequiredService<IOptions<GraphOptions>>().Value);
    }

    [Fact]
    public void Missing_Section_IsTreatedAsDisabled()
    {
        using var provider = Build([]);

        Assert.IsType<DisabledGraphClient>(provider.GetRequiredService<IGraphClient>());
    }

    [Fact]
    public void Enabled_RegistersTheRealClientAndSharedTokenProvider()
    {
        using var provider = Build(new()
        {
            ["Graph:Enabled"] = "true",
            ["Graph:TenantId"] = "11111111-1111-1111-1111-111111111111",
            ["Graph:ClientId"] = "22222222-2222-2222-2222-222222222222",
            ["Graph:ClientSecret"] = "placeholder"
        });

        Assert.IsType<GraphClient>(provider.GetRequiredService<IGraphClient>());
        Assert.Same(provider.GetRequiredService<IGraphTokenProvider>(), provider.GetRequiredService<IGraphTokenProvider>());
    }

    [Fact]
    public void Enabled_WithMissingCredentials_FailsOptionsValidation()
    {
        using var provider = Build(new() { ["Graph:Enabled"] = "true", ["Graph:TenantId"] = "tenant" });

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GraphOptions>>().Value);

        Assert.Contains(exception.Failures, failure => failure.Contains("ClientId", StringComparison.Ordinal));
        Assert.Contains(exception.Failures, failure => failure.Contains("ClientSecret", StringComparison.Ordinal));
        Assert.DoesNotContain(exception.Failures, failure => failure.Contains("TenantId", StringComparison.Ordinal));
    }

    [Fact]
    public void Disabled_IgnoresIncompleteCredentials()
    {
        using var provider = Build(new() { ["Graph:Enabled"] = "false", ["Graph:TenantId"] = "tenant" });

        Assert.NotNull(provider.GetRequiredService<IOptions<GraphOptions>>().Value);
    }

    [Fact]
    public void AddGraphClient_RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => GraphServiceCollectionExtensions.AddGraphClient(null!, new ConfigurationBuilder().Build()));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddGraphClient(null!));
    }
}
