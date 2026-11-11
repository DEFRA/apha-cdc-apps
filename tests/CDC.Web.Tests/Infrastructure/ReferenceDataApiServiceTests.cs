using System.Net;
using System.Text;
using System.Text.Json;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class ReferenceDataApiServiceTests
{
    private static readonly Guid TableId = Guid.Parse("D9889426-96A2-4C69-A3DB-C26F256C5FC1");
    private static readonly Guid ValueId = Guid.Parse("533262C1-454C-45E3-876B-2B47841AB5C2");

    [Fact]
    public async Task GetTablesAsync_And_GetTableAsync_ReadTheMaintainableTables()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, $$"""[{"id":"{{TableId}}","name":"Control mechanism"}]""");
        var service = CreateService(handler);

        var table = Assert.Single(await service.GetTablesAsync());
        Assert.Equal("Control mechanism", table.Name);
        Assert.Equal("/api/reference-data/maintainable-tables", handler.LastRequestUri!.AbsolutePath);
        Assert.Equal(TableId, (await service.GetTableAsync(TableId))!.Id);
        Assert.Null(await service.GetTableAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetValuesAsync_MapsDatesFlagsAndSequence()
    {
        const string json = """
            [
              {"id":"533262c1-454c-45e3-876b-2b47841ab5c2","lookupValue":"Import controls","effectiveDateFrom":"2007-01-01T00:00:00","effectiveDateTo":null,"isInUse":true,"isActive":true},
              {"id":"39d5ae2d-29c9-4958-8a83-ba9646b6b136","lookupValue":"Culling","effectiveDateFrom":"2007-01-01T00:00:00","effectiveDateTo":"2026-11-09T10:30:00","isInUse":false,"isActive":false}
            ]
            """;
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);

        var values = await CreateService(handler).GetValuesAsync(TableId);

        Assert.Equal($"/api/reference-data/maintainable-tables/{TableId}/values", handler.LastRequestUri!.AbsolutePath);
        Assert.Equal(2, values.Count);
        Assert.Equal(new DateOnly(2007, 1, 1), values[0].EffectiveDateFrom);
        Assert.True(values[0].IsActive);
        Assert.True(values[0].IsInUse);
        Assert.Equal((byte)1, values[0].SequenceNumber);
        Assert.Equal(new DateOnly(2026, 11, 9), values[1].EffectiveDateTo);
        Assert.False(values[1].IsActive);
        Assert.Equal((byte)2, values[1].SequenceNumber);
    }

    [Fact]
    public async Task GetValuesAsync_ReturnsEmpty_WhenTheTableIsNotMaintainable()
    {
        var values = await CreateService(new RecordingHttpMessageHandler(HttpStatusCode.NotFound, "{}")).GetValuesAsync(TableId);

        Assert.Empty(values);
    }

    [Fact]
    public async Task GetValueAsync_ReturnsTheRowVersion_OrNullWhenNotFound()
    {
        var json = $$"""{"id":"{{ValueId}}","lookupValue":"Import controls","isInUse":true,"isActive":true,"lastUpdated":"AAAAAAAAB9E="}""";
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);

        var value = await CreateService(handler).GetValueAsync(TableId, ValueId);

        Assert.Equal($"/api/reference-data/maintainable-tables/{TableId}/values/{ValueId}", handler.LastRequestUri!.AbsolutePath);
        Assert.Equal(Convert.FromBase64String("AAAAAAAAB9E="), value!.LastUpdated);
        Assert.Null(await CreateService(new RecordingHttpMessageHandler(HttpStatusCode.NotFound, "{}")).GetValueAsync(TableId, ValueId));
    }

    [Fact]
    public async Task UpdateValueAsync_PostsTheChangeWithoutAValueIdWhenAdding()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, """{"valueId":"00000000-0000-0000-0000-000000000001"}""");

        var result = await CreateService(handler).UpdateValueAsync(
            new ReferenceDataUpdate(TableId, ValueId, "Zoning", "New control type", "Ignored", ReferenceDataChangeAction.Add));

        Assert.Equal(ReferenceDataUpdateOutcome.Updated, result.Outcome);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/reference-data/maintainable-tables/{TableId}/changes", handler.LastRequestUri!.AbsolutePath);

        using var body = JsonDocument.Parse(handler.LastRequestBody!);
        Assert.Equal(1, body.RootElement.GetProperty("action").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("valueId").ValueKind);
        Assert.Equal("Zoning", body.RootElement.GetProperty("newLookupValue").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "{}", ReferenceDataUpdateOutcome.ValueNotFound)]
    [InlineData(HttpStatusCode.Conflict, "{}", ReferenceDataUpdateOutcome.Conflict)]
    [InlineData(HttpStatusCode.BadRequest, """{"detail":"There is already a reference value with this name"}""", ReferenceDataUpdateOutcome.DuplicateLookupValue)]
    public async Task UpdateValueAsync_MapsFailureResponses(HttpStatusCode statusCode, string body, ReferenceDataUpdateOutcome expected)
    {
        var result = await CreateService(new RecordingHttpMessageHandler(statusCode, body)).UpdateValueAsync(
            new ReferenceDataUpdate(TableId, ValueId, "Zoning", "Reason", "Ignored", ReferenceDataChangeAction.Edit, [1, 2, 3, 4, 5, 6, 7, 8]));

        Assert.Equal(expected, result.Outcome);
    }

    [Fact]
    public async Task UpdateValueAsync_Throws_ForAnyOtherBadRequest()
    {
        var service = CreateService(new RecordingHttpMessageHandler(HttpStatusCode.BadRequest, """{"detail":"Something else"}"""));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.UpdateValueAsync(
            new ReferenceDataUpdate(TableId, ValueId, "Zoning", "Reason", "Ignored")));
    }

    [Fact]
    public async Task GetAuditTrailAsync_And_GetAuditEntryAsync_ReadOneTablesTrail()
    {
        var auditId = Guid.NewGuid();
        var json = $$"""
            [{"id":"{{auditId}}","referenceTableId":"{{TableId}}","tableName":"Control mechanism","oldLookupValue":"- new entry -",
              "newLookupValue":"Zoning","userFullName":"Profile Editor","effectiveDate":"2026-11-09T10:30:00","logDate":"2026-11-09T10:30:00","reason":"New"}]
            """;
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);
        var service = CreateService(handler);

        var entry = Assert.Single(await service.GetAuditTrailAsync(TableId));
        Assert.Equal($"/api/reference-data/maintainable-tables/{TableId}/audit-trail", handler.LastRequestUri!.AbsolutePath);
        Assert.Equal(new DateTime(2026, 11, 9, 10, 30, 0), entry.LogDate);
        Assert.Equal("Control mechanism", entry.TableName);
        Assert.Equal(auditId, (await service.GetAuditEntryAsync(TableId, auditId))!.Id);
        Assert.Null(await service.GetAuditEntryAsync(TableId, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetAuditTrailAsync_ReturnsEmpty_WhenTheTableIsNotMaintainable()
    {
        var entries = await CreateService(new RecordingHttpMessageHandler(HttpStatusCode.NotFound, "{}")).GetAuditTrailAsync(TableId);

        Assert.Empty(entries);
    }

    private static ReferenceDataApiService CreateService(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") });

    private sealed class RecordingHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        public HttpMethod? LastMethod { get; private set; }

        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastMethod = request.Method;
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
