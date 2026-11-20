using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Hosting;

namespace CDC.Web.Tests.Integration;

public class ServiceKeyConfigurationTests : IClassFixture<CdcWebTestFactory>
{
    private readonly CdcWebTestFactory _factory;

    public ServiceKeyConfigurationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Startup_Fails_WhenTheServiceKeyIsBlank(string serviceKey)
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseSetting("Api:ServiceKey", serviceKey));

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("Api:ServiceKey", exception.Message, StringComparison.Ordinal);
    }
}
