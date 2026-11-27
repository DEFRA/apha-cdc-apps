using System.Net;
using System.Text;
using System.Text.Json;
using CDC.Web.Infrastructure;

namespace CDC.Web.Tests.Infrastructure;

public class CrossCuttingIssueScoreApiServiceTests
{
    private static readonly Guid ValueId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task GetMetadataAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            [{"id":"11111111-1111-1111-1111-111111111111","name":"Movements","sequenceNumber":2,"criteria":[
              {"id":"22222222-2222-2222-2222-222222222222","name":"Ease","sequenceNumber":1,"values":[
                {"id":"33333333-3333-3333-3333-333333333333","value":"Unique id","score":45,"sequenceNumber":1}]}]}]
            """;
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);

        var categories = await CreateService(handler).GetMetadataAsync();

        var category = Assert.Single(categories);
        Assert.Equal("Movements", category.Name);
        var value = Assert.Single(Assert.Single(category.Criteria).Values);
        Assert.Equal(ValueId, value.Id);
        Assert.Equal(45, value.Score);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal("/api/cross-cutting-issue-scores", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetMetadataAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new RecordingHttpMessageHandler(HttpStatusCode.OK, "null"));

        Assert.Empty(await service.GetMetadataAsync());
    }

    [Fact]
    public async Task GetMetadataAsync_Throws_OnFailureStatusCode()
    {
        var service = CreateService(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.GetMetadataAsync());
    }

    [Fact]
    public async Task SaveAsync_PutsTheScoresToTheEndpoint()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.NoContent, string.Empty);

        await CreateService(handler).SaveAsync(new Dictionary<Guid, int> { [ValueId] = 7 });

        Assert.Equal(HttpMethod.Put, handler.LastMethod);
        Assert.Equal("/api/cross-cutting-issue-scores", handler.LastRequestUri!.AbsolutePath);
        using var body = JsonDocument.Parse(handler.LastBody!);
        var score = Assert.Single(body.RootElement.GetProperty("scores").EnumerateArray());
        Assert.Equal(ValueId, score.GetProperty("valueId").GetGuid());
        Assert.Equal(7, score.GetProperty("score").GetInt32());
    }

    [Fact]
    public async Task SaveAsync_Throws_OnFailureStatusCode()
    {
        var service = CreateService(new RecordingHttpMessageHandler(HttpStatusCode.BadRequest, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => service.SaveAsync(new Dictionary<Guid, int> { [ValueId] = 7 }));
    }

    [Fact]
    public async Task SaveAsync_Throws_WhenScoresAreNull()
    {
        var service = CreateService(new RecordingHttpMessageHandler(HttpStatusCode.NoContent, string.Empty));

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.SaveAsync(null!));
    }

    private static CrossCuttingIssueScoreApiService CreateService(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") });

    private sealed class RecordingHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        public HttpMethod? LastMethod { get; private set; }

        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;
            LastMethod = request.Method;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
