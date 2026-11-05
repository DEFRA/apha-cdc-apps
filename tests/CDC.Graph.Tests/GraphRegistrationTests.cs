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
    public void Missing_Section_UsesSharedEntraValues_WhenAvailable()
    {
        using var provider = Build(new()
        {
            ["Entra:TenantId"] = "11111111-1111-1111-1111-111111111111",
            ["Entra:ClientId"] = "22222222-2222-2222-2222-222222222222",
            ["Entra:ClientSecret"] = "placeholder"
        });

        Assert.IsType<GraphClient>(provider.GetRequiredService<IGraphClient>());
        Assert.Same(provider.GetRequiredService<IGraphTokenProvider>(), provider.GetRequiredService<IGraphTokenProvider>());
    }

    [Fact]
    public void Enabled_RegistersTheRealClientAndSharedTokenProvider()
    {
        using var provider = Build(new()
        {
            ["Graph:TenantId"] = "11111111-1111-1111-1111-111111111111",
            ["Graph:ClientId"] = "22222222-2222-2222-2222-222222222222",
            ["Graph:ClientSecret"] = "placeholder"
        });

        Assert.IsType<GraphClient>(provider.GetRequiredService<IGraphClient>());
        Assert.Same(provider.GetRequiredService<IGraphTokenProvider>(), provider.GetRequiredService<IGraphTokenProvider>());
    }

    [Fact]
    public void Enabled_UsesSharedEntraValues_WhenGraphValuesAbsent()
    {
        using var provider = Build(new()
        {
            ["Graph:Enabled"] = "true",
            ["Entra:TenantId"] = "11111111-1111-1111-1111-111111111111",
            ["Entra:ClientId"] = "22222222-2222-2222-2222-222222222222",
            ["Entra:ClientSecret"] = "shared-entra-client-secret"
        });

        var options = provider.GetRequiredService<IOptions<GraphOptions>>().Value;

        Assert.Equal("11111111-1111-1111-1111-111111111111", options.TenantId);
        Assert.Equal("22222222-2222-2222-2222-222222222222", options.ClientId);
        Assert.Equal("shared-entra-client-secret", options.ClientSecret);
    }

    [Fact]
    public void Enabled_WithMissingCredentials_FailsOptionsValidation()
    {
        using var provider = Build(new() { ["Graph:TenantId"] = "tenant" });

        var exception = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<GraphOptions>>().Value);

        Assert.Contains(exception.Failures, failure => failure.Contains("ClientId", StringComparison.Ordinal));
        Assert.Contains(exception.Failures, failure => failure.Contains("ClientSecret", StringComparison.Ordinal));
        Assert.DoesNotContain(exception.Failures, failure => failure.Contains("TenantId", StringComparison.Ordinal));
    }

    [Fact]
    public void AddGraphClient_RejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => GraphServiceCollectionExtensions.AddGraphClient(null!, new ConfigurationBuilder().Build()));
        Assert.Throws<ArgumentNullException>(() => new ServiceCollection().AddGraphClient(null!));
    }
}
