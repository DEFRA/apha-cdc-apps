using System.Net;
using System.Net.Http.Json;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileManagement.Interfaces;
using CDC.Api.Tests.ProfileManagement;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace CDC.Api.Tests.Endpoints;

// Sends the request the web app sends, so the routing, model binding and validation are the real ones.
public class ProfileStatusEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ProfileStatusEndpointTests(WebApplicationFactory<Program> factory)
    {
        Environment.SetEnvironmentVariable("Database__Host", "localhost");
        Environment.SetEnvironmentVariable("Database__Name", "cdc-tests");
        Environment.SetEnvironmentVariable("Database__User", "cdc-tests");
        Environment.SetEnvironmentVariable("Database__Password", "cdc-tests-password");
        Environment.SetEnvironmentVariable("HealthCheck__ReadinessKey", "local-dev-readiness-key");

        _factory = factory;
    }

    [Fact]
    public async Task PutStatus_SetsTheStatus_WhenTheBodyCarriesOnlyTheStatus()
    {
        IReadOnlyList<ProfileStatusType> statusTypes =
            [new ProfileStatusType { Id = ProfileManagementTestData.ProfileStatusId, Name = "Draft", IsValidationComplete = false }];
        var repository = new Mock<IProfileManagementRepository>();
        repository
            .Setup(repo => repo.GetProfileStatusTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(statusTypes);
        var client = _factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProfileManagementRepository>();
                services.AddSingleton(repository.Object);
            }))
            .CreateClient();

        var response = await client.PutAsJsonAsync(
            $"/api/profiles/{ProfileManagementTestData.ProfileId}/status",
            new { ProfileStatusId = ProfileManagementTestData.ProfileStatusId });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        repository.Verify(
            repo => repo.UpdateProfileStatusAsync(
                ProfileManagementTestData.ProfileId,
                ProfileManagementTestData.ProfileStatusId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
