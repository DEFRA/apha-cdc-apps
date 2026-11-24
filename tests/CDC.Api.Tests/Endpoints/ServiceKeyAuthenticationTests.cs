using System.Net;
using System.Net.Http.Json;
using CDC.Api.Features.Users.Interfaces;
using CDC.Common.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace CDC.Api.Tests.Endpoints;

public class ServiceKeyAuthenticationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ReferenceValuesPath = "/api/reference-data/5a2f4bc6-0a2d-4b55-9d0b-4f0a6c1f2d11/values";

    private readonly WebApplicationFactory<Program> _factory;
    private readonly Mock<IUserRepository> _users = new(MockBehavior.Strict);

    public ServiceKeyAuthenticationTests(WebApplicationFactory<Program> factory)
    {
        Environment.SetEnvironmentVariable("Database__Host", "localhost");
        Environment.SetEnvironmentVariable("Database__Name", "cdc-tests");
        Environment.SetEnvironmentVariable("Database__User", "cdc-tests");
        Environment.SetEnvironmentVariable("Database__Password", "cdc-tests-password");
        Environment.SetEnvironmentVariable("HealthCheck__ReadinessKey", "local-dev-readiness-key");
        TestServiceKey.Configure();

        _factory = factory;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("wrong-key")]
    [InlineData("test-service-key-and-more")]
    public async Task AnEndpointWithoutARoleCheck_IsRefused_WithoutTheRightServiceKey(string? serviceKey)
    {
        var client = CreateClient();

        if (serviceKey is not null)
        {
            client.DefaultRequestHeaders.Add(ServiceKeyHeader.Name, serviceKey);
        }

        var response = await client.GetAsync(ReferenceValuesPath);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TheServiceKey_IsRefused_WhenSentTwice()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(ServiceKeyHeader.Name, [TestServiceKey.Value, TestServiceKey.Value]);

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task TheSignInResolveEndpoint_IsRefused_WithoutTheServiceKey()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/users/internal/resolve",
            new { SsoUserIdInt = Guid.NewGuid(), UserName = "someone", FullName = "Some One" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // The strict user repository proves the acting user is never even looked up for an unauthenticated caller.
    [Fact]
    public async Task TheActingUserHeader_IsIgnored_WithoutTheServiceKey()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(ActingUserHeader.Name, Guid.NewGuid().ToString());

        var response = await client.GetAsync(ReferenceValuesPath);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        _users.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ARequest_IsServed_WithTheServiceKey()
    {
        var client = CreateClient().WithServiceKey();

        var response = await client.GetAsync("/");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Liveness_StaysOpen_WithoutTheServiceKey()
    {
        var client = CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private HttpClient CreateClient() =>
        _factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IUserRepository>();
                services.AddSingleton(_users.Object);
            }))
            .CreateClient();
}
