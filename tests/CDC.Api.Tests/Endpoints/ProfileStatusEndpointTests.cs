using System.Net;
using System.Net.Http.Json;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileManagement.Interfaces;
using CDC.Api.Features.Users.Interfaces;
using CDC.Api.Tests.ProfileManagement;
using CDC.Common.Auth;
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
        var repository = StatusRepository();
        var client = CreateClient(repository, ActingUser(isProfileEditor: true));

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PutStatus_IsForbidden_WhenTheActingUserIsNotAProfileEditor(bool isPolicyProfileUser)
    {
        var repository = StatusRepository();
        var client = CreateClient(repository, ActingUser(isProfileEditor: false, isPolicyProfileUser));

        var response = await client.PutAsJsonAsync(
            $"/api/profiles/{ProfileManagementTestData.ProfileId}/status",
            new { ProfileStatusId = ProfileManagementTestData.ProfileStatusId });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        repository.Verify(
            repo => repo.UpdateProfileStatusAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PutStatus_IsForbidden_WhenTheRequestNamesNoActingUser()
    {
        var repository = StatusRepository();
        var client = CreateClient(repository, actingUser: null);

        var response = await client.PutAsJsonAsync(
            $"/api/profiles/{ProfileManagementTestData.ProfileId}/status",
            new { ProfileStatusId = ProfileManagementTestData.ProfileStatusId });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static Mock<IProfileManagementRepository> StatusRepository()
    {
        IReadOnlyList<ProfileStatusType> statusTypes =
            [new ProfileStatusType { Id = ProfileManagementTestData.ProfileStatusId, Name = "Draft", IsValidationComplete = false }];
        var repository = new Mock<IProfileManagementRepository>();
        repository
            .Setup(repo => repo.GetProfileStatusTypesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(statusTypes);

        return repository;
    }

    private static ActingUser ActingUser(bool isProfileEditor, bool isPolicyProfileUser = false) => new()
    {
        Id = Guid.NewGuid(),
        IsProfileEditor = isProfileEditor,
        IsPolicyProfileUser = isPolicyProfileUser,
        SsoUserIdInt = Guid.NewGuid()
    };

    private HttpClient CreateClient(Mock<IProfileManagementRepository> repository, ActingUser? actingUser)
    {
        var users = new Mock<IUserRepository>();

        if (actingUser is not null)
        {
            users
                .Setup(repo => repo.GetActingUserAsync(actingUser.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(actingUser);
        }

        var client = _factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProfileManagementRepository>();
                services.AddSingleton(repository.Object);
                services.RemoveAll<IUserRepository>();
                services.AddSingleton(users.Object);
            }))
            .CreateClient();

        if (actingUser is not null)
        {
            client.DefaultRequestHeaders.Add(ActingUserHeader.Name, actingUser.Id.ToString());
        }

        return client;
    }
}
