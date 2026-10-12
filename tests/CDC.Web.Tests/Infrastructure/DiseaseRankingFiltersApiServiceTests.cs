using System.Net;
using System.Text;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class DiseaseRankingFiltersApiServiceTests
{
    [Fact]
    public async Task GetFiltersAsync_DeserialisesTheResponseBody()
    {
        const string json = """[{"filterNumber":1,"diseaseName":"Anthrax"}]""";
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.OK, json));

        var filters = await client.GetFiltersAsync();

        var filter = Assert.Single(filters);
        Assert.Equal("Anthrax", filter.DiseaseName);
    }

    [Fact]
    public async Task GetFiltersAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.OK, "null"));

        var filters = await client.GetFiltersAsync();

        Assert.Empty(filters);
    }

    [Fact]
    public async Task GetFilterDetailAsync_ReturnsNull_WhenTheFilterIsNotFound()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.NotFound, string.Empty));

        var detail = await client.GetFilterDetailAsync(99);

        Assert.Null(detail);
    }

    [Fact]
    public async Task GetFilterDetailAsync_Throws_OnFailureStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetFilterDetailAsync(1));
    }

    [Fact]
    public async Task GetFilterDetailAsync_DeserialisesTheResponseBody()
    {
        const string json = """{"filterNumber":1,"diseaseName":"Anthrax","disease1":"both","disease2":"both","disease3":"both","disease4":"both","diseaseTypes":[],"speciesIds":[]}""";
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.OK, json));

        var detail = await client.GetFilterDetailAsync(1);

        Assert.Equal("Anthrax", detail?.DiseaseName);
    }

    [Fact]
    public async Task CreateFilterAsync_PostsToTheFiltersEndpoint()
    {
        const string json = """{"filterNumber":7,"diseaseName":"Anthrax"}""";
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var result = await client.CreateFilterAsync(new SaveDiseaseRankingFilterRequestDto { DiseaseName = "Anthrax" });

        Assert.Equal("/api/disease-ranking-filters", handler.LastRequestUri!.AbsolutePath);
        Assert.Equal(DiseaseRankingFilterSaveOutcome.Success, result.Outcome);
        Assert.Equal(7, result.FilterNumber);
    }

    [Fact]
    public async Task UpdateFilterAsync_PutsToTheFilterEndpoint()
    {
        const string json = """{"filterNumber":7,"diseaseName":"Anthrax"}""";
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, json);
        var client = CreateClient(handler);

        var result = await client.UpdateFilterAsync(7, new SaveDiseaseRankingFilterRequestDto { DiseaseName = "Anthrax" });

        Assert.Equal("/api/disease-ranking-filters/7", handler.LastRequestUri!.AbsolutePath);
        Assert.Equal(DiseaseRankingFilterSaveOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task SaveFilter_ReturnsDuplicateNameOutcome_OnConflict()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.Conflict, string.Empty));

        var result = await client.CreateFilterAsync(new SaveDiseaseRankingFilterRequestDto { DiseaseName = "Anthrax" });

        Assert.Equal(DiseaseRankingFilterSaveOutcome.DuplicateName, result.Outcome);
    }

    [Fact]
    public async Task SaveFilter_ReturnsValidationFailedOutcome_OnBadRequest()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.BadRequest, string.Empty));

        var result = await client.CreateFilterAsync(new SaveDiseaseRankingFilterRequestDto { DiseaseName = "Anthrax" });

        Assert.Equal(DiseaseRankingFilterSaveOutcome.ValidationFailed, result.Outcome);
    }

    [Fact]
    public async Task SaveFilter_ReturnsErrorOutcome_OnAnyOtherFailureStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        var result = await client.CreateFilterAsync(new SaveDiseaseRankingFilterRequestDto { DiseaseName = "Anthrax" });

        Assert.Equal(DiseaseRankingFilterSaveOutcome.Error, result.Outcome);
    }

    [Fact]
    public async Task DeleteFilterAsync_DeletesTheFilterEndpoint()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.NoContent, string.Empty);
        var client = CreateClient(handler);

        await client.DeleteFilterAsync(7);

        Assert.Equal("/api/disease-ranking-filters/7", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task DeleteFilterAsync_Throws_OnFailureStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.DeleteFilterAsync(7));
    }

    private static DiseaseRankingFiltersApiService CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") };

        return new DiseaseRankingFiltersApiService(httpClient);
    }

    private sealed class RecordingHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
