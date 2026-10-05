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
                      "isPerSpecies": true,
                      "isRepeating": true,
                      "fields": [
                        {
                          "id": "2e3f4a5b-6c7d-8e9f-0a1b-2c3d4e5f6071",
                          "name": "Affected species",
                          "shortName": "Species",
                          "dataTypeName": "List",
                          "isMandatory": true,
                          "dataFieldTypeId": "3a4b5c6d-7e8f-9a0b-1c2d-3e4f5a6b7c8d",
                          "referenceTableId": "4b5c6d7e-8f9a-0b1c-2d3e-4f5a6b7c8d9e",
                          "referenceTableIsMaintainable": true
                        }
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
        Assert.True(question.IsPerSpecies);
        Assert.True(question.IsRepeating);
        var field = Assert.Single(question.Fields);
        Assert.Equal("Affected species", field.Name);
        Assert.True(field.IsMandatory);
        Assert.True(field.ReferenceTableIsMaintainable);
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
              "questionNames": [ { "id": "2e3f4a5b-6c7d-8e9f-0a1b-2c3d4e5f6071", "name": "Is it endemic?", "nonTechnicalName": "Endemic?" } ],
              "fieldValues": [
                {
                  "id": "3f4a5b6c-7d8e-9f0a-1b2c-3d4e5f607182",
                  "questionId": "2e3f4a5b-6c7d-8e9f-0a1b-2c3d4e5f6071",
                  "fieldNumber": 1,
                  "booleanValue": true,
                  "listValue": "4a5b6c7d-8e9f-0a1b-2c3d-4e5f60718293",
                  "textValue": "Some notes",
                  "decimalValue": 1.5,
                  "dateValue": "2026-01-01T00:00:00Z"
                }
              ]
            }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var answers = await service.GetProfileSectionAnswersAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Single(answers.QuestionNames);
        var fieldValue = Assert.Single(answers.FieldValues);
        Assert.True(fieldValue.BooleanValue);
        Assert.Equal(1.5m, fieldValue.DecimalValue);
        Assert.Equal(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), fieldValue.DateValue);
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

    [Fact]
    public async Task GetProfileNoteTypesAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            [{ "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f", "name": "Scientific paper reference", "pluralName": "Scientific paper references" }]
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var noteTypes = await service.GetProfileNoteTypesAsync();

        var noteType = Assert.Single(noteTypes);
        Assert.Equal("Scientific paper reference", noteType.Name);
    }

    [Fact]
    public async Task GetProfileNoteTypesAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var noteTypes = await service.GetProfileNoteTypesAsync();

        Assert.Empty(noteTypes);
    }

    [Fact]
    public async Task GetProfileNotesBySectionAsync_DeserialisesTheResponseBody()
    {
        const string json = """[{ "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f", "noteText": "A relevant paper." }]""";
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var notes = await service.GetProfileNotesBySectionAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        var note = Assert.Single(notes);
        Assert.Equal("A relevant paper.", note.NoteText);
    }

    [Fact]
    public async Task GetProfileNotesBySectionAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var notes = await service.GetProfileNotesBySectionAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Assert.Empty(notes);
    }

    private static ProfileSectionsApiService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") };

        return new ProfileSectionsApiService(httpClient);
    }
}
