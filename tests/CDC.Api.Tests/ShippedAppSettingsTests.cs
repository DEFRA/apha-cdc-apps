#using CDC.Graph.Options;
using Microsoft.Extensions.Configuration;

namespace CDC.Api.Tests;

public class ShippedAppSettingsTests
{
    // GraphOptions is validated when the host starts, so an environment that was never given Graph credentials only starts if the
    // settings the image ships with need none.
    [Fact]
    public void Graph_NeedsNoCredentials_UnderTheShippedDefaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(AppContext.BaseDirectory, "appsettings.json"), optional: false)
            .Build();
        var options = configuration.GetSection(GraphOptions.SectionName).Get<GraphOptions>() ?? new GraphOptions();

        var result = new GraphOptionsValidator().Validate(null, options);

        Assert.True(result.Succeeded, string.Join("; ", result.Failures ?? []));
    }
}
