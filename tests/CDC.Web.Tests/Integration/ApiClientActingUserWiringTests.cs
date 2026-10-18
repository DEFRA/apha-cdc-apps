using CDC.Web.Infrastructure;
using CDC.Web.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;

namespace CDC.Web.Tests.Integration;

// CDC.Api records changes against, and authorises them for, the user CDC.Web forwards - so every typed client that
// calls it must carry ActingUserDelegatingHandler. A client registered without it would reach the API as anonymous.
public class ApiClientActingUserWiringTests : IClassFixture<CdcWebTestFactory>
{
    private readonly CdcWebTestFactory _factory;

    public ApiClientActingUserWiringTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<string> ApiClientNames()
    {
        var names = new TheoryData<string>();

        foreach (var name in DiscoverApiClientNames())
        {
            names.Add(name);
        }

        return names;
    }

    // Every typed client takes an HttpClient and is registered under its interface, named I + the class name.
    private static List<string> DiscoverApiClientNames() =>
    [
        .. typeof(ApiClient).Assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && type.GetConstructors().Any(constructor => constructor.GetParameters().Any(parameter => parameter.ParameterType == typeof(HttpClient))))
            .Select(type => $"I{type.Name}")
            .Order(StringComparer.Ordinal)
    ];

    [Fact]
    public void TheTypedApiClients_AreFound()
    {
        Assert.True(DiscoverApiClientNames().Count >= 12, "Expected at least the twelve typed CDC.Api clients.");
    }

    [Theory]
    [MemberData(nameof(ApiClientNames))]
    public void EveryTypedApiClient_ForwardsTheSignedInUser(string clientName)
    {
        var handler = _factory.Services.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(clientName);

        Assert.Contains(Chain(handler), link => link is ActingUserDelegatingHandler);
    }

    private static IEnumerable<HttpMessageHandler> Chain(HttpMessageHandler handler)
    {
        for (var link = handler; link is not null; link = (link as DelegatingHandler)?.InnerHandler)
        {
            yield return link;
        }
    }
}
