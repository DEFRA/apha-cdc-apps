using System.Net;
using System.Text;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class PrioritisationVariablesApiServiceTests
{
    private static readonly Guid CriterionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ValueId = Guid.Parse("33333333-3333-3333-3333-333333333333");

    [Fact]
    public async Task GetCategoriesAsync_DeserialisesTheResponseBody()
    {
        const string json = """[{"id":"11111111-1111-1111-1111-111111111111","name":"Animal welfare","criteria":[]}]""";
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.OK, json));

        var categories = await client.GetCategoriesAsync();

        var category = Assert.Single(categories);
        Assert.Equal("Animal welfare", category.Name);
    }

    [Fact]
    public async Task GetCategoriesAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.OK, "null"));

        var categories = await client.GetCategoriesAsync();

        Assert.Empty(categories);
    }

    [Fact]
    public async Task UpdateCriterionAsync_PutsToTheCriterionEndpoint()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.NoContent, string.Empty);
        var client = CreateClient(handler);

        await client.UpdateCriterionAsync(CriterionId, 42, [new CriterionValueScore { ValueId = ValueId, Score = 5 }]);

        Assert.Equal($"/api/prioritisation-variables/criteria/{CriterionId}", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task UpdateCriterionAsync_Throws_OnFailureStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.UpdateCriterionAsync(CriterionId, 42, []));
    }

    [Fact]
    public async Task GetRankingRangeAsync_DeserialisesTheResponseBody()
    {
        const string json = """{"lowerBound":15,"upperBound":35,"rowVersion":"AQIDBAUGBwg="}""";
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.OK, json));

        var rankingRange = await client.GetRankingRangeAsync();

        Assert.Equal(15, rankingRange.LowerBound);
        Assert.Equal(35, rankingRange.UpperBound);
        Assert.Equal("AQIDBAUGBwg=", rankingRange.RowVersion);
    }

    [Fact]
    public async Task GetRankingRangeAsync_ReturnsDefault_WhenTheResponseBodyIsNull()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.OK, "null"));

        var rankingRange = await client.GetRankingRangeAsync();

        Assert.Equal(0, rankingRange.LowerBound);
    }

    [Fact]
    public async Task UpdateRankingRangeAsync_ReturnsTheSavedRange()
    {
        const string json = """{"lowerBound":15,"upperBound":35,"rowVersion":"CQoLDA0ODxA="}""";
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.OK, json));

        var saved = await client.UpdateRankingRangeAsync(new PrioritisationRankingRangeDto { LowerBound = 15, UpperBound = 35, RowVersion = "AQIDBAUGBwg=" });

        Assert.Equal("CQoLDA0ODxA=", saved.RowVersion);
    }

    [Fact]
    public async Task UpdateRankingRangeAsync_Throws_OnConflict()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.Conflict, "{}"));

        await Assert.ThrowsAsync<RankingRangeConflictException>(
            () => client.UpdateRankingRangeAsync(new PrioritisationRankingRangeDto { LowerBound = 15, UpperBound = 35, RowVersion = "AQIDBAUGBwg=" }));
    }

    [Fact]
    public async Task PrioritiseDraftProfilesAsync_PostsToTheDraftPrioritisationEndpoint()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.NoContent, string.Empty);

        await CreateClient(handler).PrioritiseDraftProfilesAsync();

        Assert.Equal("/api/prioritisation-variables/draft-prioritisation", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task PrioritiseDraftProfilesAsync_Throws_OnFailureStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.PrioritiseDraftProfilesAsync());
    }

    [Fact]
    public async Task RecalculateScoresAsync_PostsToTheScoreRecalculationEndpoint()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.NoContent, string.Empty);

        await CreateClient(handler).RecalculateScoresAsync();

        Assert.Equal("/api/prioritisation-variables/score-recalculation", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task RecalculateScoresAsync_Throws_OnFailureStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.RecalculateScoresAsync());
    }

    [Fact]
    public async Task ExportCategoryScoreDataAsync_ReturnsTheFileWithItsDispositionName()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, "a,b", "Welfare.csv");
        var categoryId = Guid.NewGuid();

        var export = await CreateClient(handler).ExportCategoryScoreDataAsync(categoryId);

        Assert.NotNull(export);
        Assert.Equal("Welfare.csv", export.FileName);
        Assert.Equal("a,b", Encoding.UTF8.GetString(export.Content));
        Assert.Equal($"/api/prioritisation-variables/categories/{categoryId}/score-data", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ExportCategoryScoreDataAsync_FallsBackToTheDefaultFileName_WhenNoDispositionIsReturned()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.OK, "a,b"));

        var export = await client.ExportCategoryScoreDataAsync(Guid.NewGuid());

        Assert.Equal("ScoreData.csv", export!.FileName);
    }

    [Fact]
    public async Task ExportCategoryScoreDataAsync_ReturnsNull_WhenTheCategoryIsNotFound()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.NotFound, string.Empty));

        Assert.Null(await client.ExportCategoryScoreDataAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task ExportNormalisedScoresAsync_ReturnsTheFile()
    {
        var handler = new RecordingHttpMessageHandler(HttpStatusCode.OK, "a,b");

        var export = await CreateClient(handler).ExportNormalisedScoresAsync();

        Assert.Equal("NormalisedScoreSummary.csv", export.FileName);
        Assert.Equal("/api/prioritisation-variables/normalised-scores", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task ExportNormalisedScoresAsync_Throws_OnFailureStatusCode()
    {
        var client = CreateClient(new RecordingHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.ExportNormalisedScoresAsync());
    }

    private static PrioritisationVariablesApiService CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") };

        return new PrioritisationVariablesApiService(httpClient);
    }

    private sealed class RecordingHttpMessageHandler(HttpStatusCode statusCode, string responseBody, string? fileName = null) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            var response = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };

            if (fileName is not null)
            {
                response.Content.Headers.ContentDisposition = new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment") { FileName = fileName };
            }

            return Task.FromResult(response);
        }
    }
}
