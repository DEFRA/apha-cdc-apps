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

    [Fact]
    public async Task GetProfileNotesByVersionAsync_DeserialisesTheResponseBody()
    {
        const string json = """[{ "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f", "noteText": "A relevant paper." }]""";
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var notes = await service.GetProfileNotesByVersionAsync(Guid.NewGuid(), Guid.NewGuid());

        var note = Assert.Single(notes);
        Assert.Equal("A relevant paper.", note.NoteText);
    }

    [Fact]
    public async Task GetProfileNotesByVersionAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var notes = await service.GetProfileNotesByVersionAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Empty(notes);
    }

    [Fact]
    public async Task GetProfileVersionValidationStatusAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            {
              "profileVersionId": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
              "sections": [{ "profileSectionId": "1f2e3d4c-5b6a-7988-9a0b-1c2d3e4f5a6b", "isValid": true }]
            }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var status = await service.GetProfileVersionValidationStatusAsync(Guid.NewGuid());

        var section = Assert.Single(status.Sections);
        Assert.True(section.IsValid);
    }

    [Fact]
    public async Task GetProfileVersionValidationStatusAsync_ReturnsEmptySections_WhenTheResponseBodyIsNull()
    {
        var profileVersionId = Guid.NewGuid();
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var status = await service.GetProfileVersionValidationStatusAsync(profileVersionId);

        Assert.Equal(profileVersionId, status.ProfileVersionId);
        Assert.Empty(status.Sections);
    }

    [Fact]
    public async Task ValidateProfileVersionSectionAsync_DeserialisesTheResponseBody()
    {
        const string json = """{ "isValid": false, "unansweredQuestions": ["3.1 Is it endemic?"] }""";
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var result = await service.ValidateProfileVersionSectionAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result.IsValid);
        Assert.Equal("3.1 Is it endemic?", Assert.Single(result.UnansweredQuestions));
    }

    [Fact]
    public async Task ValidateProfileVersionSectionAsync_ReturnsValid_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var result = await service.ValidateProfileVersionSectionAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task ClearProfileVersionSectionAsync_DeserialisesTheResponseBody()
    {
        const string json = """{ "fieldsClearedCount": 4 }""";
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var result = await service.ClearProfileVersionSectionAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(4, result.FieldsClearedCount);
    }

    [Fact]
    public async Task ClearProfileVersionSectionAsync_ReturnsDefault_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var result = await service.ClearProfileVersionSectionAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(0, result.FieldsClearedCount);
    }

    [Fact]
    public async Task ClearProfileVersionSectionAsync_Throws_OnANonSuccessStatusCode()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.ClearProfileVersionSectionAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task SaveProfileVersionSectionAsync_DeserialisesTheResponseBody()
    {
        const string json = """{ "fieldsSavedCount": 2 }""";
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var result = await service.SaveProfileVersionSectionAsync(Guid.NewGuid(), Guid.NewGuid(), []);

        Assert.Equal(2, result.FieldsSavedCount);
    }

    [Fact]
    public async Task SaveProfileVersionSectionAsync_ReturnsDefault_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var result = await service.SaveProfileVersionSectionAsync(Guid.NewGuid(), Guid.NewGuid(), []);

        Assert.Equal(0, result.FieldsSavedCount);
    }

    [Fact]
    public async Task SaveProfileVersionSectionAsync_Throws_OnANonSuccessStatusCode()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.BadRequest, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.SaveProfileVersionSectionAsync(Guid.NewGuid(), Guid.NewGuid(), []));
    }

    [Fact]
    public async Task GetProfileVersionSectionContributionsAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            [{ "userId": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f", "userName": "jdoe", "organisationName": "APHA", "fullName": "Jane Doe", "lastContributionDate": "2026-09-11T00:00:00" }]
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var contributions = await service.GetProfileVersionSectionContributionsAsync(Guid.NewGuid(), Guid.NewGuid());

        var contribution = Assert.Single(contributions);
        Assert.Equal("jdoe", contribution.UserName);
        Assert.Equal("Jane Doe", contribution.FullName);
    }

    [Fact]
    public async Task GetProfileVersionSectionContributionsAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var contributions = await service.GetProfileVersionSectionContributionsAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Empty(contributions);
    }

    [Fact]
    public async Task GetProfileSectionReviewCommentsAsync_DeserialisesTheResponseBody()
    {
        const string json = """[{ "commentSubject": "A comment", "userFullName": "Jane Doe", "level": 1 }]""";
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var comments = await service.GetProfileSectionReviewCommentsAsync(Guid.NewGuid(), Guid.NewGuid());

        var comment = Assert.Single(comments);
        Assert.Equal("A comment", comment.CommentSubject);
        Assert.Equal("Jane Doe", comment.UserFullName);
    }

    [Fact]
    public async Task GetProfileSectionReviewCommentsAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var comments = await service.GetProfileSectionReviewCommentsAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Empty(comments);
    }

    [Fact]
    public async Task GetProfileVersionAllRevisionDatesAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            {
              "profileVersionId": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
              "sections": [{ "sectionName": "Summary", "sectionNumber": 1, "technicalReviewFrequencyMonths": 12 }]
            }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var result = await service.GetProfileVersionAllRevisionDatesAsync(Guid.NewGuid());

        var section = Assert.Single(result.Sections);
        Assert.Equal("Summary", section.SectionName);
        Assert.Equal(12, section.TechnicalReviewFrequencyMonths);
    }

    [Fact]
    public async Task GetProfileVersionAllRevisionDatesAsync_ReturnsEmptySections_WhenTheResponseBodyIsNull()
    {
        var profileVersionId = Guid.NewGuid();
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var result = await service.GetProfileVersionAllRevisionDatesAsync(profileVersionId);

        Assert.Equal(profileVersionId, result.ProfileVersionId);
        Assert.Empty(result.Sections);
    }

    private static ProfileSectionsApiService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") };

        return new ProfileSectionsApiService(httpClient);
    }
}
