using System.Net;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class ProfileQuestionsApiServiceTests
{
    private static readonly Guid QuestionId = Guid.Parse("6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f");
    private static readonly UpdateProfileQuestionRequest Request = new("Name", "Plain name", "<p>Guidance</p>", [1, 2, 3, 4, 5, 6, 7, 8]);

    [Fact]
    public async Task GetProfileQuestionAsync_ReadsTheQuestionFromItsUrl()
    {
        const string json = """
            {
              "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
              "name": "Is it present?",
              "shortName": "Present",
              "nonTechnicalName": "Is it here?",
              "questionNumber": 4,
              "userGuidance": "Say yes.",
              "lastUpdated": "AQIDBAUGBwg="
            }
            """;
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);

        var question = await CreateService(handler).GetProfileQuestionAsync(QuestionId);

        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal($"/api/profile-questions/{QuestionId}", handler.LastUri!.AbsolutePath);
        Assert.NotNull(question);
        Assert.Equal("Is it present?", question.Name);
        Assert.Equal("Is it here?", question.NonTechnicalName);
        Assert.Equal(4, question.QuestionNumber);
        Assert.Equal("Say yes.", question.UserGuidance);
        Assert.Equal<byte>([1, 2, 3, 4, 5, 6, 7, 8], question.LastUpdated);
    }

    [Fact]
    public async Task GetProfileQuestionAsync_ReturnsNull_WhenThereIsNoSuchQuestion()
    {
        var question = await CreateService(new FakeHttpMessageHandler(HttpStatusCode.NotFound, "{}")).GetProfileQuestionAsync(QuestionId);

        Assert.Null(question);
    }

    [Fact]
    public async Task GetProfileQuestionAsync_Throws_WhenTheApiFails() =>
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateService(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "{}")).GetProfileQuestionAsync(QuestionId));

    [Fact]
    public async Task GetProfileGuidanceContentAsync_ReadsTheSectionsAndPrioritisationCategories()
    {
        const string json = """
            {
              "sections": [
                {
                  "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
                  "sectionNumber": 2,
                  "name": "Epidemiology",
                  "questions": [
                    { "id": "1f2e3d4c-5b6a-7988-9a0b-1c2d3e4f5a6b", "questionNumber": 7, "name": "Q", "nonTechnicalName": "Plain", "userGuidance": "Say yes." }
                  ]
                }
              ],
              "prioritisationCategories": [
                {
                  "id": "2e3f4a5b-6c7d-8e9f-0a1b-2c3d4e5f6071",
                  "displayName": "Welfare",
                  "criteria": [
                    { "profileQuestionId": "1f2e3d4c-5b6a-7988-9a0b-1c2d3e4f5a6b", "criterionCode": "W01", "criterionName": "Suffering" }
                  ]
                }
              ]
            }
            """;
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, json);

        var result = await CreateService(handler).GetProfileGuidanceContentAsync();

        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal("/api/profile-questions/guidance-content", handler.LastUri!.AbsolutePath);
        Assert.Equal(ProfileGuidanceContentOutcome.Success, result.Outcome);
        var section = Assert.Single(result.Content!.Sections);
        Assert.Equal(2, section.SectionNumber);
        Assert.Equal("Epidemiology", section.Name);
        var question = Assert.Single(section.Questions);
        Assert.Equal(7, question.QuestionNumber);
        Assert.Equal("Plain", question.NonTechnicalName);
        Assert.Equal("Say yes.", question.UserGuidance);
        var category = Assert.Single(result.Content.PrioritisationCategories);
        Assert.Equal("Welfare", category.DisplayName);
        Assert.Equal("W01", Assert.Single(category.Criteria).CriterionCode);
    }

    [Fact]
    public async Task GetProfileGuidanceContentAsync_ReturnsEmptyContent_WhenTheBodyIsNull()
    {
        var result = await CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null")).GetProfileGuidanceContentAsync();

        Assert.Equal(ProfileGuidanceContentOutcome.Success, result.Outcome);
        Assert.Empty(result.Content!.Sections);
        Assert.Empty(result.Content.PrioritisationCategories);
    }

    [Fact]
    public async Task GetProfileGuidanceContentAsync_ReportsForbidden_WhenTheApiRefuses()
    {
        var result = await CreateService(new FakeHttpMessageHandler(HttpStatusCode.Forbidden, "{}")).GetProfileGuidanceContentAsync();

        Assert.Equal(ProfileGuidanceContentOutcome.Forbidden, result.Outcome);
        Assert.Null(result.Content);
    }

    [Fact]
    public async Task GetProfileGuidanceContentAsync_Throws_WhenTheApiFails() =>
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            CreateService(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "{}")).GetProfileGuidanceContentAsync());

    [Fact]
    public async Task UpdateProfileQuestionAsync_PutsTheChangesWithTheRowVersion()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "{}");

        var result = await CreateService(handler).UpdateProfileQuestionAsync(QuestionId, Request);

        Assert.Equal(ProfileQuestionUpdateOutcome.Success, result.Outcome);
        Assert.Null(result.ErrorMessage);
        Assert.Equal(HttpMethod.Put, handler.LastMethod);
        Assert.Equal($"/api/profile-questions/{QuestionId}", handler.LastUri!.AbsolutePath);
        Assert.Contains("\"name\":\"Name\"", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("\"nonTechnicalName\":\"Plain name\"", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("\"userGuidance\":\"\\u003Cp\\u003EGuidance\\u003C/p\\u003E\"", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("\"lastUpdated\":\"AQIDBAUGBwg=\"", handler.LastRequestBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, ProfileQuestionUpdateOutcome.Conflict, ProfileQuestionsApiService.ConflictMessage)]
    [InlineData(HttpStatusCode.Forbidden, ProfileQuestionUpdateOutcome.Forbidden, ProfileQuestionsApiService.ForbiddenMessage)]
    [InlineData(HttpStatusCode.NotFound, ProfileQuestionUpdateOutcome.NotFound, ProfileQuestionsApiService.NotFoundMessage)]
    [InlineData(HttpStatusCode.InternalServerError, ProfileQuestionUpdateOutcome.Error, ProfileQuestionsApiService.ErrorMessage)]
    public async Task UpdateProfileQuestionAsync_MapsEachFailureToItsOutcome(HttpStatusCode status, ProfileQuestionUpdateOutcome outcome, string message)
    {
        var result = await CreateService(new FakeHttpMessageHandler(status, "{}")).UpdateProfileQuestionAsync(QuestionId, Request);

        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(message, result.ErrorMessage);
    }

    [Fact]
    public async Task UpdateProfileQuestionAsync_ReportsTheFirstValidationMessage_FromABadRequest()
    {
        const string problem = """{ "title": "Validation failed", "errors": { "Name": ["The name must be 400 characters or fewer."] } }""";

        var result = await CreateService(new FakeHttpMessageHandler(HttpStatusCode.BadRequest, problem)).UpdateProfileQuestionAsync(QuestionId, Request);

        Assert.Equal(ProfileQuestionUpdateOutcome.ValidationFailed, result.Outcome);
        Assert.Equal("The name must be 400 characters or fewer.", result.ErrorMessage);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{ "errors": {} }""")]
    [InlineData("not json")]
    public async Task UpdateProfileQuestionAsync_FallsBackToAGenericMessage_WhenABadRequestGivesNoReason(string body)
    {
        var result = await CreateService(new FakeHttpMessageHandler(HttpStatusCode.BadRequest, body)).UpdateProfileQuestionAsync(QuestionId, Request);

        Assert.Equal(ProfileQuestionUpdateOutcome.ValidationFailed, result.Outcome);
        Assert.Equal(ProfileQuestionsApiService.ValidationFallbackMessage, result.ErrorMessage);
    }

    private static ProfileQuestionsApiService CreateService(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") });
}
