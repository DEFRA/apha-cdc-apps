using System.Net;
using CDC.Web.Infrastructure;

namespace CDC.Web.Tests.Infrastructure;

public class SpeciesApiServiceTests
{
    [Fact]
    public async Task GetAllSpeciesAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            [
              {
                "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
                "parentId": "00000000-0000-0000-0000-000000000000",
                "description": "Cattle",
                "isActive": true,
                "isInUse": true
              }
            ]
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var species = await service.GetAllSpeciesAsync();

        var item = Assert.Single(species);
        Assert.Equal("Cattle", item.Description);
        Assert.True(item.IsActive);
    }

    [Fact]
    public async Task GetAllSpeciesAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var species = await service.GetAllSpeciesAsync();

        Assert.Empty(species);
    }

    [Fact]
    public async Task GetAllSpeciesAsync_Throws_OnANonSuccessStatusCode()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetAllSpeciesAsync());
    }

    [Fact]
    public async Task GetSpeciesDetailAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            {
              "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
              "name": "Cattle",
              "parentId": "00000000-0000-0000-0000-000000000000",
              "parentName": "",
              "isActive": true,
              "isInUse": true,
              "childCount": 0,
              "activeChildCount": 0,
              "lastUpdated": "AAAAAAAAAAE="
            }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var detail = await service.GetSpeciesDetailAsync(Guid.NewGuid());

        Assert.NotNull(detail);
        Assert.Equal("Cattle", detail!.Name);
    }

    [Fact]
    public async Task GetSpeciesDetailAsync_ReturnsNull_WhenNotFound()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.NotFound, string.Empty));

        var detail = await service.GetSpeciesDetailAsync(Guid.NewGuid());

        Assert.Null(detail);
    }

    [Fact]
    public async Task GetSpeciesValidParentsAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            [ { "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f", "name": "Cattle" } ]
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var validParents = await service.GetSpeciesValidParentsAsync(Guid.NewGuid());

        var item = Assert.Single(validParents);
        Assert.Equal("Cattle", item.Name);
    }

    [Fact]
    public async Task UpdateSpeciesNameParentAsync_ReturnsSuccess_OnOk()
    {
        const string json = """
            { "speciesId": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f", "lastUpdated": "AAAAAAAAAAI=" }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var result = await service.UpdateSpeciesNameParentAsync(new CDC.Web.Models.UpdateSpeciesNameParentRequestDto());

        Assert.Equal(CDC.Web.Models.SpeciesUpdateOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task UpdateSpeciesNameParentAsync_ReturnsConflict_OnHttp409()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.Conflict, string.Empty));

        var result = await service.UpdateSpeciesNameParentAsync(new CDC.Web.Models.UpdateSpeciesNameParentRequestDto());

        Assert.Equal(CDC.Web.Models.SpeciesUpdateOutcome.Conflict, result.Outcome);
    }

    [Fact]
    public async Task UpdateSpeciesNameParentAsync_ReturnsValidationFailed_OnHttp400()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.BadRequest, string.Empty));

        var result = await service.UpdateSpeciesNameParentAsync(new CDC.Web.Models.UpdateSpeciesNameParentRequestDto());

        Assert.Equal(CDC.Web.Models.SpeciesUpdateOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task UpdateSpeciesNameParentAsync_ReturnsError_OnUnexpectedStatusCode()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        var result = await service.UpdateSpeciesNameParentAsync(new CDC.Web.Models.UpdateSpeciesNameParentRequestDto());

        Assert.Equal(CDC.Web.Models.SpeciesUpdateOutcome.Error, result.Outcome);
    }

    [Fact]
    public async Task GetSpeciesAuditTrailAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            [
              {
                "id": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
                "oldName": "Dairy cattle",
                "newName": "Dairy",
                "oldParent": "Cattle",
                "newParent": "Cattle",
                "changedBy": "a.user",
                "logDate": "2026-01-01T00:00:00",
                "reasonForChange": "Simplifying the name"
              }
            ]
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var entries = await service.GetSpeciesAuditTrailAsync();

        var entry = Assert.Single(entries);
        Assert.Equal("Dairy", entry.NewName);
    }

    [Fact]
    public async Task GetSpeciesMetadataAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            {
              "sections": [
                {
                  "id": "1f2e3d4c-5b6a-7988-9a0b-1c2d3e4f5a6b",
                  "name": "Movements",
                  "shortName": "Move",
                  "sectionNumber": 2,
                  "questions": [
                    {
                      "id": "2f3e4d5c-6b7a-8988-9a0b-1c2d3e4f5a6c",
                      "sectionId": "1f2e3d4c-5b6a-7988-9a0b-1c2d3e4f5a6b",
                      "name": "Can movements be traced?",
                      "shortName": "Traceable",
                      "questionNumber": 1,
                      "fields": [
                        {
                          "id": "3f4e5d6c-7b8a-9988-9a0b-1c2d3e4f5a6d",
                          "questionId": "2f3e4d5c-6b7a-8988-9a0b-1c2d3e4f5a6c",
                          "name": "Yes/No",
                          "fieldNumber": 1,
                          "dataTypeName": "Boolean",
                          "isMandatory": true,
                          "editorFieldType": 2,
                          "dataFieldTypeId": "4f5e6d7c-8b9a-0988-9a0b-1c2d3e4f5a6e",
                          "referenceTableId": "5f6e7d8c-9b0a-1988-9a0b-1c2d3e4f5a6f",
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

        var metadata = await service.GetSpeciesMetadataAsync();

        var section = Assert.Single(metadata.Sections);
        Assert.Equal("Movements", section.Name);
        var question = Assert.Single(section.Questions);
        Assert.Equal("Can movements be traced?", question.Name);
        var field = Assert.Single(question.Fields);
        Assert.True(field.IsMandatory);
        Assert.Equal(2, field.EditorFieldType);
        Assert.True(field.ReferenceTableIsMaintainable);
    }

    [Fact]
    public async Task GetSpeciesMetadataAsync_ReturnsEmpty_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var metadata = await service.GetSpeciesMetadataAsync();

        Assert.Empty(metadata.Sections);
    }

    [Fact]
    public async Task GetSpeciesAnswerDataAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            {
              "speciesId": "6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f",
              "speciesName": "Cattle",
              "lastUpdated": "AAAAAAAAAAE=",
              "sections": [
                {
                  "sectionId": "1f2e3d4c-5b6a-7988-9a0b-1c2d3e4f5a6b",
                  "fieldValues": [
                    { "id": "4f5e6d7c-8b9a-a988-9a0b-1c2d3e4f5a6e", "questionId": "2f3e4d5c-6b7a-8988-9a0b-1c2d3e4f5a6c", "fieldNumber": 1, "booleanValue": true }
                  ]
                }
              ]
            }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var answerData = await service.GetSpeciesAnswerDataAsync(Guid.NewGuid());

        Assert.NotNull(answerData);
        Assert.Equal("Cattle", answerData!.SpeciesName);
        var section = Assert.Single(answerData.Sections);
        var value = Assert.Single(section.FieldValues);
        Assert.True(value.BooleanValue);
    }

    [Fact]
    public async Task GetSpeciesAnswerDataAsync_ReturnsNull_WhenNotFound()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.NotFound, string.Empty));

        var answerData = await service.GetSpeciesAnswerDataAsync(Guid.NewGuid());

        Assert.Null(answerData);
    }

    [Fact]
    public async Task GetReferenceValuesAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            [ { "id": "3f4a5b6c-7d8e-9f0a-1b2c-3d4e5f607182", "value": "Market records" } ]
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var values = await service.GetReferenceValuesAsync(Guid.NewGuid());

        var value = Assert.Single(values);
        Assert.Equal("Market records", value.Value);
    }

    [Fact]
    public async Task GetReferenceValuesAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var values = await service.GetReferenceValuesAsync(Guid.NewGuid());

        Assert.Empty(values);
    }

    private static SpeciesApiService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") };

        return new SpeciesApiService(httpClient);
    }
}
