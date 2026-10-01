using System.Net;
using System.Text;
using CDC.Web.Infrastructure;

namespace CDC.Web.Tests.Infrastructure;

public class ProfileSectionsApiServiceTests
{
    [Fact]
    public async Task GetProfileQuestionnaireMetadataAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            {
              "sections": [
                {
                  "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
                  "name": "Epidemiology",
                  "sectionNumber": 3,
                  "questions": [
                    {
                      "id": "1f2e3d4c-5b6a-7988-9a0b-1c2d3e4f5a6b",
                      "shortName": "Q1",
                      "questionNumber": 1,
                      "fields": [
                        { "id": "2e3f4a5b-6c7d-8e9f-0a1b-2c3d4e5f6071", "name": "Affected species", "dataTypeName": "List" }
                      ]
                    }
                  ]
                }
              ]
            }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var metadata = await service.GetProfileQuestionnaireMetadataAsync();

        var section = Assert.Single(metadata.Sections);
        Assert.Equal("Epidemiology", section.Name);
        var question = Assert.Single(section.Questions);
        Assert.Equal("Q1", question.ShortName);
        var field = Assert.Single(question.Fields);
        Assert.Equal("Affected species", field.Name);
    }

    [Fact]
    public async Task GetProfileQuestionnaireMetadataAsync_ReturnsEmptyMetadata_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var metadata = await service.GetProfileQuestionnaireMetadataAsync();

        Assert.Empty(metadata.Sections);
    }

    [Fact]
    public async Task GetProfileSectionAnswersAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            {
              "profileVersionId": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
              "profileSectionId": "1f2e3d4c-5b6a-7988-9a0b-1c2d3e4f5a6b",
              "questionNames": [ { "id": "2e3f4a5b-6c7d-8e9f-0a1b-2c3d4e5f6071", "name": "Is it endemic?" } ],
              "fieldValues": [ { "id": "3f4a5b6c-7d8e-9f0a-1b2c-3d4e5f607182", "questionId": "2e3f4a5b-6c7d-8e9f-0a1b-2c3d4e5f6071", "fieldNumber": 1, "booleanValue": true } ]
            }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var answers = await service.GetProfileSectionAnswersAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Single(answers.QuestionNames);
        var fieldValue = Assert.Single(answers.FieldValues);
        Assert.True(fieldValue.BooleanValue);
    }

    [Fact]
    public async Task GetProfileSectionAnswersAsync_ReturnsEmptyAnswers_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var answers = await service.GetProfileSectionAnswersAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Empty(answers.QuestionNames);
        Assert.Empty(answers.FieldValues);
    }

    [Fact]
    public async Task GetReferenceValuesAsync_DeserialisesTheResponseBody()
    {
        const string json = """[{ "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f", "value": "Cattle" }]""";
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var values = await service.GetReferenceValuesAsync(Guid.NewGuid());

        Assert.Single(values);
        Assert.Equal("Cattle", values[0].Value);
    }

    [Fact]
    public async Task GetReferenceValuesAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var values = await service.GetReferenceValuesAsync(Guid.NewGuid());

        Assert.Empty(values);
    }

    private static ProfileSectionsApiService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") };

        return new ProfileSectionsApiService(httpClient);
    }

    private sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
    }
}
