using System.Net;
using System.Net.Http.Json;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileQuestions.Commands;
using CDC.Api.Features.ProfileQuestions.Interfaces;
using CDC.Api.Features.Users.Interfaces;
using CDC.Api.Tests.ProfileQuestions;
using CDC.Common.Auth;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace CDC.Api.Tests.Endpoints;

// Sends the request the web app sends, so the routing, model binding and validation are the real ones.
public class ProfileQuestionEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public ProfileQuestionEndpointTests(WebApplicationFactory<Program> factory)
    {
        Environment.SetEnvironmentVariable("Database__Host", "localhost");
        Environment.SetEnvironmentVariable("Database__Name", "cdc-tests");
        Environment.SetEnvironmentVariable("Database__User", "cdc-tests");
        Environment.SetEnvironmentVariable("Database__Password", "cdc-tests-password");
        Environment.SetEnvironmentVariable("HealthCheck__ReadinessKey", "local-dev-readiness-key");
        TestServiceKey.Configure();

        _factory = factory;
    }

    [Fact]
    public async Task PutQuestion_SavesTheQuestion_WhenTheBodyCarriesNoId()
    {
        var repository = QuestionRepository();
        var client = CreateClient(repository, ActingUser(isProfileEditor: true));

        var response = await client.PutAsJsonAsync(
            $"/api/profile-questions/{ProfileQuestionTestData.QuestionId}",
            new
            {
                Name = "Is <i>Brucella</i> present?",
                NonTechnicalName = "Is it here?",
                UserGuidance = "<p>Say yes.</p>",
                LastUpdated = ProfileQuestionTestData.RowVersion
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        repository.Verify(
            repo => repo.UpdateProfileQuestionAsync(
                It.Is<UpdateProfileQuestionCommand>(command =>
                    command.Id == ProfileQuestionTestData.QuestionId
                    && command.Name == "Is <i>Brucella</i> present?"
                    && command.NonTechnicalName == "Is it here?"
                    && command.UserGuidance == "<p>Say yes.</p>"
                    && command.LastUpdated.SequenceEqual(ProfileQuestionTestData.RowVersion)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PutQuestion_UsesTheRouteId_WhenTheBodyNamesAnotherQuestion()
    {
        var repository = QuestionRepository();
        var client = CreateClient(repository, ActingUser(isProfileEditor: true));

        var response = await client.PutAsJsonAsync(
            $"/api/profile-questions/{ProfileQuestionTestData.QuestionId}",
            new
            {
                Id = Guid.NewGuid(),
                Name = "Name",
                NonTechnicalName = "Plain",
                UserGuidance = "Guidance",
                LastUpdated = ProfileQuestionTestData.RowVersion
            });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        repository.Verify(
            repo => repo.UpdateProfileQuestionAsync(
                It.Is<UpdateProfileQuestionCommand>(command => command.Id == ProfileQuestionTestData.QuestionId),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PutQuestion_IsForbidden_WhenTheActingUserIsNotAProfileEditor()
    {
        var repository = QuestionRepository();
        var client = CreateClient(repository, ActingUser(isProfileEditor: false));

        var response = await client.PutAsJsonAsync(
            $"/api/profile-questions/{ProfileQuestionTestData.QuestionId}",
            new { Name = "Name", NonTechnicalName = "Plain", UserGuidance = "Guidance", LastUpdated = ProfileQuestionTestData.RowVersion });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        repository.Verify(
            repo => repo.UpdateProfileQuestionAsync(It.IsAny<UpdateProfileQuestionCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static Mock<IProfileQuestionRepository> QuestionRepository()
    {
        var repository = new Mock<IProfileQuestionRepository>();
        repository
            .Setup(repo => repo.UpdateProfileQuestionAsync(It.IsAny<UpdateProfileQuestionCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ProfileQuestionTestData.ProfileQuestion());

        return repository;
    }

    private static ActingUser ActingUser(bool isProfileEditor) => new()
    {
        Id = Guid.NewGuid(),
        IsProfileEditor = isProfileEditor,
        IsPolicyProfileUser = false,
        SsoUserIdInt = Guid.NewGuid()
    };

    private HttpClient CreateClient(Mock<IProfileQuestionRepository> repository, ActingUser actingUser)
    {
        var users = new Mock<IUserRepository>();
        users
            .Setup(repo => repo.GetActingUserAsync(actingUser.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(actingUser);

        var client = _factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IProfileQuestionRepository>();
                services.AddSingleton(repository.Object);
                services.RemoveAll<IUserRepository>();
                services.AddSingleton(users.Object);
            }))
            .CreateClient()
            .WithServiceKey();

        client.DefaultRequestHeaders.Add(ActingUserHeader.Name, actingUser.Id.ToString());

        return client;
    }
}
