using System.Net;
using System.Text;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

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
              "sections": [{
                "profileSectionId": "1f2e3d4c-5b6a-7988-9a0b-1c2d3e4f5a6b",
                "isValid": false,
                "invalidQuestionIds": ["2a3b4c5d-6e7f-4809-91a2-b3c4d5e6f708"]
              }]
            }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var status = await service.GetProfileVersionValidationStatusAsync(Guid.NewGuid());

        var section = Assert.Single(status.Sections);
        Assert.False(section.IsValid);
        Assert.Equal(Guid.Parse("2a3b4c5d-6e7f-4809-91a2-b3c4d5e6f708"), Assert.Single(section.InvalidQuestionIds));
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
        const string json = """
            {
              "isValid": false,
              "invalidReasons": [{
                "questionId": "2a3b4c5d-6e7f-4809-91a2-b3c4d5e6f708",
                "sectionNumber": 3,
                "questionNumber": 1,
                "questionShortName": "Endemic",
                "description": "\"Is it endemic? - Endemic\" is required"
              }]
            }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var result = await service.ValidateProfileVersionSectionAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result.IsValid);
        var reason = Assert.Single(result.InvalidReasons);
        Assert.Equal(Guid.Parse("2a3b4c5d-6e7f-4809-91a2-b3c4d5e6f708"), reason.QuestionId);
        Assert.Equal(3, reason.SectionNumber);
        Assert.Equal(1, reason.QuestionNumber);
        Assert.Equal("Endemic", reason.QuestionShortName);
        Assert.Equal("\"Is it endemic? - Endemic\" is required", reason.Description);
    }

    [Fact]
    public async Task ValidateProfileVersionSectionAsync_ReturnsValid_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var result = await service.ValidateProfileVersionSectionAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task GetProfileVersionValidationStatusAsync_DeserialisesTheIrrelevantQuestions()
    {
        const string json = """
            {
              "profileVersionId": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
              "sections": [{
                "profileSectionId": "1f2e3d4c-5b6a-7988-9a0b-1c2d3e4f5a6b",
                "isValid": true,
                "irrelevantQuestionIds": ["3b4c5d6e-7f80-4910-a2b3-c4d5e6f70819"]
              }]
            }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var status = await service.GetProfileVersionValidationStatusAsync(Guid.NewGuid());

        Assert.Equal(Guid.Parse("3b4c5d6e-7f80-4910-a2b3-c4d5e6f70819"), Assert.Single(Assert.Single(status.Sections).IrrelevantQuestionIds));
    }

    [Fact]
    public async Task GetProfileSectionAnswersAsync_DeserialisesTheSectionsRowVersion()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, """{ "lastUpdated": "AQIDBAUGBwg=" }"""));

        var answers = await service.GetProfileSectionAnswersAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, answers.LastUpdated);
    }

    [Fact]
    public async Task GetProfileQuestionnaireMetadataAsync_DeserialisesTheReadOnlyFlags()
    {
        const string json = """
            { "sections": [{ "questions": [{ "fields": [
              { "name": "Profiled species", "isReadOnlyCurrentSituation": true, "isReadOnlyScenario": false },
              { "name": "Other", "isReadOnlyCurrentSituation": false, "isReadOnlyScenario": true }
            ] }] }] }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var metadata = await service.GetProfileQuestionnaireMetadataAsync();

        var fields = metadata.Sections.Single().Questions.Single().Fields;
        Assert.True(fields[0].IsReadOnlyFor(isWhatIfScenario: false));
        Assert.False(fields[0].IsReadOnlyFor(isWhatIfScenario: true));
        Assert.False(fields[1].IsReadOnlyFor(isWhatIfScenario: false));
        Assert.True(fields[1].IsReadOnlyFor(isWhatIfScenario: true));
    }

    [Fact]
    public async Task ClearProfileVersionSectionAsync_ReturnsSuccessWithTheClearedCount_AndSendsTheRowVersion()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{ "fieldsClearedCount": 4 }""");
        var service = CreateService(handler);

        var result = await service.ClearProfileVersionSectionAsync(Guid.NewGuid(), Guid.NewGuid(), [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Equal(ProfileSectionWriteOutcome.Success, result.Outcome);
        Assert.Equal(4, result.FieldsChangedCount);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("/api/profile-sections/clear", handler.LastUri!.AbsolutePath);
        Assert.Contains("\"lastUpdated\":\"AQIDBAUGBwg=\"", handler.LastRequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ClearProfileVersionSectionAsync_SendsNoRowVersion_WhenThereIsNone()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, "null");
        var service = CreateService(handler);

        var result = await service.ClearProfileVersionSectionAsync(Guid.NewGuid(), Guid.NewGuid(), null);

        Assert.Equal(ProfileSectionWriteOutcome.Success, result.Outcome);
        Assert.Equal(0, result.FieldsChangedCount);
        Assert.Contains("\"lastUpdated\":null", handler.LastRequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveProfileVersionSectionAsync_ReturnsSuccessWithTheSavedCount_AndSendsTheAnswersAndTheRowVersion()
    {
        var fieldId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(HttpStatusCode.OK, """{ "fieldsSavedCount": 2 }""");
        var service = CreateService(handler);

        var result = await service.SaveProfileVersionSectionAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            [new CDC.Common.Contracts.ProfileFieldAnswerInputDto { ProfileFieldId = fieldId, TextValue = "Some notes" }],
            [1, 2, 3, 4, 5, 6, 7, 8]);

        Assert.Equal(ProfileSectionWriteOutcome.Success, result.Outcome);
        Assert.Equal(2, result.FieldsChangedCount);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("/api/profile-sections/save", handler.LastUri!.AbsolutePath);
        Assert.Contains("\"lastUpdated\":\"AQIDBAUGBwg=\"", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains("Some notes", handler.LastRequestBody, StringComparison.Ordinal);
        Assert.Contains(fieldId.ToString(), handler.LastRequestBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveProfileVersionSectionAsync_ReturnsSuccessWithNoChanges_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var result = await service.SaveProfileVersionSectionAsync(Guid.NewGuid(), Guid.NewGuid(), [], null);

        Assert.Equal(ProfileSectionWriteOutcome.Success, result.Outcome);
        Assert.Equal(0, result.FieldsChangedCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveAndClear_ClassifyAConflict_KeepingTheReasonCdcApiGave(bool clear)
    {
        const string problem = """{ "title": "Conflicting change", "status": 409, "detail": "The profile version section has been edited by another user" }""";
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.Conflict, problem));

        var result = clear
            ? await service.ClearProfileVersionSectionAsync(Guid.NewGuid(), Guid.NewGuid(), null)
            : await service.SaveProfileVersionSectionAsync(Guid.NewGuid(), Guid.NewGuid(), [], null);

        Assert.Equal(ProfileSectionWriteOutcome.Conflict, result.Outcome);
        Assert.Equal("The profile version section has been edited by another user", result.ErrorMessage);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "")]
    [InlineData(HttpStatusCode.BadRequest, "<html>Bad request</html>")]
    [InlineData(HttpStatusCode.InternalServerError, """{ "title": "Server error", "status": 500 }""")]
    public async Task SaveAndClear_FallBackToAGenericReason_WhenTheFailureHasNoDetail(HttpStatusCode statusCode, string body)
    {
        var service = CreateService(new FakeHttpMessageHandler(statusCode, body));

        var saved = await service.SaveProfileVersionSectionAsync(Guid.NewGuid(), Guid.NewGuid(), [], null);
        var cleared = await service.ClearProfileVersionSectionAsync(Guid.NewGuid(), Guid.NewGuid(), null);

        Assert.Equal(ProfileSectionWriteOutcome.Error, saved.Outcome);
        Assert.Equal("We could not complete the request. Try again later.", saved.ErrorMessage);
        Assert.Equal(ProfileSectionWriteOutcome.Error, cleared.Outcome);
        Assert.Equal("We could not complete the request. Try again later.", cleared.ErrorMessage);
    }

    [Fact]
    public async Task DeleteAllProfileReviewCommentsAsync_DeletesTheSectionsCommentsAndReportsSuccess()
    {
        var profileVersionId = Guid.NewGuid();
        var profileSectionId = Guid.NewGuid();
        var handler = new FakeHttpMessageHandler(HttpStatusCode.NoContent, string.Empty);
        var service = CreateService(handler);

        var result = await service.DeleteAllProfileReviewCommentsAsync(profileVersionId, profileSectionId);

        Assert.Equal(ProfileSectionWriteOutcome.Success, result.Outcome);
        Assert.Equal(HttpMethod.Delete, handler.LastMethod);
        Assert.Equal("/api/profile-sections/review-comments", handler.LastUri!.AbsolutePath);
        Assert.Contains($"profileVersionId={profileVersionId}", handler.LastUri.Query, StringComparison.Ordinal);
        Assert.Contains($"profileSectionId={profileSectionId}", handler.LastUri.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteAllProfileReviewCommentsAsync_KeepsTheReasonCdcApiGave_WhenItIsRefused()
    {
        const string problem = """{ "title": "Not permitted", "status": 403, "detail": "Review comments can only be deleted from the current draft version." }""";
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.Forbidden, problem));

        var result = await service.DeleteAllProfileReviewCommentsAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal(ProfileSectionWriteOutcome.Error, result.Outcome);
        Assert.Equal("Review comments can only be deleted from the current draft version.", result.ErrorMessage);
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
    public async Task GetProfileVersionAllRevisionDatesAsync_DeserialisesSectionsAndLookups()
    {
        const string json = """
            {
              "profileVersionId": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
              "sections": [{
                "sectionName": "Summary", "sectionNumber": 1,
                "vaReviewFrequencyId": "11111111-1111-1111-1111-111111111111",
                "nextAuthorReview": "2027-01-01T00:00:00",
                "lastUpdated": "AQIDBAUGBwg="
              }],
              "reviewFrequencies": [{ "id": "11111111-1111-1111-1111-111111111111", "name": "Every year" }],
              "reviewStatuses": [{ "id": "44444444-4444-4444-4444-444444444444", "name": "Complete" }]
            }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var result = await service.GetProfileVersionAllRevisionDatesAsync(Guid.NewGuid());

        var section = Assert.Single(result.Sections);
        Assert.Equal("Summary", section.SectionName);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), section.VaReviewFrequencyId);
        Assert.Equal(new DateTime(2027, 1, 1), section.NextAuthorReview);
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, section.LastUpdated);
        Assert.Equal("Every year", Assert.Single(result.ReviewFrequencies).Name);
        Assert.Equal("Complete", Assert.Single(result.ReviewStatuses).Name);
    }

    [Fact]
    public async Task SaveProfileVersionRevisionDatesAsync_ReturnsSuccessWithTheSavedCount()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, """{ "sectionsSavedCount": 3 }"""));

        var result = await service.SaveProfileVersionRevisionDatesAsync(Guid.NewGuid(), []);

        Assert.Equal(SaveRevisionDatesOutcome.Success, result.Outcome);
        Assert.Equal(3, result.SectionsSavedCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict, SaveRevisionDatesOutcome.Conflict)]
    [InlineData(HttpStatusCode.BadRequest, SaveRevisionDatesOutcome.ValidationFailed)]
    [InlineData(HttpStatusCode.InternalServerError, SaveRevisionDatesOutcome.Error)]
    public async Task SaveProfileVersionRevisionDatesAsync_ClassifiesFailureStatusCodes(HttpStatusCode statusCode, SaveRevisionDatesOutcome expected)
    {
        var service = CreateService(new FakeHttpMessageHandler(statusCode, "{}"));

        var result = await service.SaveProfileVersionRevisionDatesAsync(Guid.NewGuid(), []);

        Assert.Equal(expected, result.Outcome);
        Assert.False(string.IsNullOrEmpty(result.ErrorMessage));
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
