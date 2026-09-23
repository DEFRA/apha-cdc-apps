using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CDC.Web.Tests.Integration;

public class LandingPageSmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public LandingPageSmokeTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Landing/Internal")]
    [InlineData("/Landing/External")]
    [InlineData("/DiseaseProfiles/Search")]
    [InlineData("/DiseaseProfiles/Create")]
    [InlineData("/DiseaseProfiles/CompareVersions")]
    [InlineData("/DiseaseProfiles/ReviewTimings")]
    [InlineData("/Reports/General")]
    [InlineData("/Reports/QuestionsGuidance")]
    [InlineData("/Reports/DiseaseRanking")]
    [InlineData("/SpeciesData/Maintain")]
    [InlineData("/ViewSpeciesData")]
    [InlineData("/CrossProfileAdmin/CrossCuttingIssueScores")]
    [InlineData("/CrossProfileAdmin/PrioritisationVariables")]
    [InlineData("/CrossProfileAdmin/ReferenceData")]
    [InlineData("/HelpSupport/HelpUsingD2R2")]
    [InlineData("/HelpSupport/QualityStatement")]
    [InlineData("/UserAdmin/ExternalUsers")]
    [InlineData("/UserAdmin/GlobalUsers")]
    public async Task LandingRoutes_ReturnSuccess(string url)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task QualityStatementDownload_ReturnsPdfDocument()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/HelpSupport/QualityStatement?download=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.NotNull(response.Content.Headers.ContentDisposition);
        Assert.Contains("D2R2-Quality-Statement.pdf", response.Content.Headers.ContentDisposition!.FileName ?? string.Empty);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(bytes.Take(4).ToArray()));
    }
}
