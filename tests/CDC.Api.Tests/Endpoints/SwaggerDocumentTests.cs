using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CDC.Api.Tests.Endpoints;

public class SwaggerDocumentTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public SwaggerDocumentTests(WebApplicationFactory<Program> factory)
    {
        Environment.SetEnvironmentVariable("Database__Host", "localhost");
        Environment.SetEnvironmentVariable("Database__Name", "cdc-tests");
        Environment.SetEnvironmentVariable("Database__User", "cdc-tests");
        Environment.SetEnvironmentVariable("Database__Password", "cdc-tests-password");
        Environment.SetEnvironmentVariable("HealthCheck__ReadinessKey", "local-dev-readiness-key");
        TestServiceKey.Configure();

        _factory = factory;
    }

    // Fails when two DTOs share a class name, which Swagger cannot tell apart as schemas.
    [Fact]
    public async Task TheOpenApiDocument_IsGenerated()
    {
        var client = _factory.CreateClient().WithServiceKey();

        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"ResolvedExternalUserDto\"", await response.Content.ReadAsStringAsync());
    }
}
