using System.Net;
using System.Text.RegularExpressions;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// Reproduces the reported bug: posting the Save handler for an existing filter without touching
// the "Transmissible agent type" checkboxes should resubmit every already-checked value, exactly
// as a real browser would (repeated form fields sharing one name). This isolates whether the bug
// is in model binding (Input.DiseaseTypes) versus anything upstream of it.
public partial class EditFiltersIntegrationTests
{
    private static readonly Guid SpeciesId = Guid.Parse("6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f");

    private static readonly DiseaseRankingFilterDetailDto ExistingFilter = new()
    {
        FilterNumber = 1,
        DiseaseName = "Cat and dog diseases",
        Disease1 = "endemic diseases",
        Disease2 = "both",
        Disease3 = "both",
        Disease4 = "both",
        DiseaseTypes = ["bacteria", "virus"],
        SpeciesIds = [SpeciesId]
    };

    [Fact]
    public async Task Save_ResubmitsEveryAlreadyCheckedDiseaseType_WhenNoneAreTouchedByTheUser()
    {
        var fakeFilters = new FakeDiseaseRankingFiltersApiService(
            filters: [new DiseaseRankingFilterSummaryDto { FilterNumber = 1, DiseaseName = "Cat and dog diseases" }],
            filterDetail: ExistingFilter);
        using var factory = CreateFactory(fakeFilters);
        var client = await SignedInClientAsync(factory);

        // Loads the real rendered page for filter 1, exactly as the browser would via "View filter".
        var page = await client.GetStringAsync("/DiseaseRanking/EditFilters?SelectedFilterNumber=1");
        var token = AntiforgeryTokenRegex().Match(page).Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token));

        // Every already-checked checkbox the real form would carry, with no user changes.
        var form = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("Input.FilterNumber", "1"),
            new("Input.DiseaseName", "Cat and dog diseases"),
            new("Input.Disease1", "endemic diseases"),
            new("Input.Disease2", "both"),
            new("Input.Disease3", "both"),
            new("Input.Disease4", "both"),
            new("Input.DiseaseTypes", "bacteria"),
            new("Input.DiseaseTypes", "virus"),
            new("SpeciesIds", SpeciesId.ToString())
        };

        using var response = await client.PostAsync(
            "/DiseaseRanking/EditFilters?handler=Save",
            new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(fakeFilters.LastUpdateRequest);
        Assert.Equal(["bacteria", "virus"], fakeFilters.LastUpdateRequest!.Value.Request.DiseaseTypes);
    }

    [Fact]
    public async Task Save_CreatesANewFilter_WithTheSpeciesCheckedByTheUser()
    {
        var fakeFilters = new FakeDiseaseRankingFiltersApiService();
        using var factory = CreateFactory(fakeFilters);
        var client = await SignedInClientAsync(factory);

        // "Create new filter" renders a blank panel with no species pre-checked, exactly as the
        // browser would see it before the user ticks anything.
        var page = await client.GetStringAsync("/DiseaseRanking/EditFilters");
        var initialToken = AntiforgeryTokenRegex().Match(page).Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(initialToken));

        using var createResponse = await client.PostAsync(
            "/DiseaseRanking/EditFilters?handler=CreateNew",
            new FormUrlEncodedContent([new KeyValuePair<string, string>("__RequestVerificationToken", initialToken)]));
        var createdPage = await createResponse.Content.ReadAsStringAsync();
        var token = AntiforgeryTokenRegex().Match(createdPage).Groups[1].Value;
        Assert.False(string.IsNullOrEmpty(token));

        // The hidden filter number field must render empty for a new filter - if it rendered
        // "0" instead, posting it back would make the Save handler treat 0 as a real (existing)
        // filter number and silently call Update instead of Create.
        var hiddenFilterNumberMatch = Regex.Match(createdPage, "name=\"Input.FilterNumber\"[^>]*?value=\"([^\"]*)\"");
        Assert.True(hiddenFilterNumberMatch.Success, "Input.FilterNumber hidden field not found in rendered page");
        Assert.Equal(string.Empty, hiddenFilterNumberMatch.Groups[1].Value);

        var form = new List<KeyValuePair<string, string>>
        {
            new("__RequestVerificationToken", token),
            new("Input.DiseaseName", "New filter"),
            new("Input.Disease1", "endemic diseases"),
            new("Input.Disease2", "both"),
            new("Input.Disease3", "both"),
            new("Input.Disease4", "both"),
            new("SpeciesIds", SpeciesId.ToString())
        };

        using var response = await client.PostAsync(
            "/DiseaseRanking/EditFilters?handler=Save",
            new FormUrlEncodedContent(form));
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(body.Contains("Please select the species from the list"), body);
        Assert.NotNull(fakeFilters.LastCreateRequest);
        Assert.Equal([SpeciesId], fakeFilters.LastCreateRequest!.SpeciesIds);
    }

    private static WebApplicationFactory<Program> CreateFactory(IDiseaseRankingFiltersApiService fakeService) =>
        new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IDiseaseRankingFiltersApiService>();
                services.AddSingleton(fakeService);
                services.RemoveAll<ISpeciesApiService>();
                services.AddSingleton<ISpeciesApiService>(new FakeSpeciesApiService(
                [
                    new SpeciesDto { Id = SpeciesId, ParentId = Guid.Empty, Description = "Cattle", IsActive = true, IsInUse = true }
                ]));
            }));

    private static async Task<HttpClient> SignedInClientAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        await client.GetAsync(CdcWebTestFactory.TestSignInPath);
        return client;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*?value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();
}
