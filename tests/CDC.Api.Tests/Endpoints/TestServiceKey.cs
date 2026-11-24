using CDC.Common.Auth;

namespace CDC.Api.Tests.Endpoints;

// The service key the endpoint tests configure on the API host and send the way CDC.Web does.
internal static class TestServiceKey
{
    public const string Value = "test-service-key";

    public static void Configure() => Environment.SetEnvironmentVariable("ServiceAuth__ApiKey", Value);

    public static HttpClient WithServiceKey(this HttpClient client)
    {
        client.DefaultRequestHeaders.Add(ServiceKeyHeader.Name, Value);

        return client;
    }
}
