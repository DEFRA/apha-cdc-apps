using System.Net;
using CDC.Web.Infrastructure;
using CDC.Web.Models;

namespace CDC.Web.Tests.Infrastructure;

public class PrioritisationVariablesApiServiceTests
{
    [Fact]
    public async Task GetCategoriesAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            [
              {
                "id": "11111111-1111-1111-1111-111111111111",
                "name": "Animal welfare",
                "criteria": [
                  {
                    "id": "22222222-2222-2222-2222-222222222222",
                    "code": "C1",
                    "name": "Impact",
                    "weight": 10,
                    "values": [ { "id": "33333333-3333-3333-3333-333333333333", "value": "N/A", "score": 5 } ]
                  }
                ]
              }
            ]
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var categories = await service.GetCategoriesAsync();

        var category = Assert.Single(categories);
        Assert.Equal("Animal welfare", category.Name);
        var criterion = Assert.Single(category.Criteria);
        Assert.Equal("C1", criterion.Code);
    }

    [Fact]
    public async Task GetCategoriesAsync_ReturnsEmptyList_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var categories = await service.GetCategoriesAsync();

        Assert.Empty(categories);
    }

    [Fact]
    public async Task UpdateCriterionAsync_CompletesSuccessfully_OnNoContent()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.NoContent, string.Empty));

        await service.UpdateCriterionAsync(
            Guid.NewGuid(),
            42,
            [new CriterionValueScore { ValueId = Guid.NewGuid(), Score = 7 }]);
    }

    [Fact]
    public async Task UpdateCriterionAsync_Throws_OnANonSuccessStatusCode()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.NotFound, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.UpdateCriterionAsync(Guid.NewGuid(), 42, []));
    }

    [Fact]
    public async Task GetRankingRangeAsync_DeserialisesTheResponseBody()
    {
        const string json = """
            { "lowerBound": 20, "upperBound": 80, "rowVersion": "AQIDBAUGBwg=" }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var rankingRange = await service.GetRankingRangeAsync();

        Assert.Equal(20, rankingRange.LowerBound);
        Assert.Equal(80, rankingRange.UpperBound);
    }

    [Fact]
    public async Task GetRankingRangeAsync_ReturnsDefault_WhenTheResponseBodyIsNull()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, "null"));

        var rankingRange = await service.GetRankingRangeAsync();

        Assert.Equal(0, rankingRange.LowerBound);
    }

    [Fact]
    public async Task UpdateRankingRangeAsync_ReturnsTheSavedRange_OnOk()
    {
        const string json = """
            { "lowerBound": 15, "upperBound": 35, "rowVersion": "CQoLDA0ODxA=" }
            """;
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.OK, json));

        var saved = await service.UpdateRankingRangeAsync(
            new PrioritisationRankingRangeDto { LowerBound = 15, UpperBound = 35, RowVersion = "AQIDBAUGBwg=" });

        Assert.Equal("CQoLDA0ODxA=", saved.RowVersion);
    }

    [Fact]
    public async Task UpdateRankingRangeAsync_Throws_OnHttp409()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.Conflict, string.Empty));

        await Assert.ThrowsAsync<RankingRangeConflictException>(() => service.UpdateRankingRangeAsync(
            new PrioritisationRankingRangeDto { LowerBound = 15, UpperBound = 35, RowVersion = "AQIDBAUGBwg=" }));
    }

    [Fact]
    public async Task UpdateRankingRangeAsync_Throws_OnANonSuccessStatusCode()
    {
        var service = CreateService(new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(() => service.UpdateRankingRangeAsync(
            new PrioritisationRankingRangeDto { LowerBound = 15, UpperBound = 35, RowVersion = "AQIDBAUGBwg=" }));
    }

    private static PrioritisationVariablesApiService CreateService(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://cdc-api.test") };

        return new PrioritisationVariablesApiService(httpClient);
    }

    private sealed class FakeHttpMessageHandler(HttpStatusCode statusCode, string responseBody) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json")
            });
    }
}
