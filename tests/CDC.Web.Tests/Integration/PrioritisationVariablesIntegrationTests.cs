using System.Net;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// The default WebApplicationFactory<Program> has no live CDC.Api, so the category dropdown and
// criterion edit panel only ever render their empty/unselected paths. This swaps in a fake
// IPrioritisationVariablesApiService with real categories/criteria/values so the category's
// criteria grid and the selected criterion's value-score table actually render.
public class PrioritisationVariablesIntegrationTests
{
    private static readonly Guid CategoryId = Guid.Parse("6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f");
    private static readonly Guid CriterionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ValueId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly IReadOnlyList<PrioritisationCategoryDto> Categories =
    [
        new PrioritisationCategoryDto
        {
            Id = CategoryId,
            Name = "Disease impact",
            Criteria =
            [
                new PrioritisationCriterionDto
                {
                    Id = CriterionId,
                    Code = "DI1",
                    Name = "Herd prevalence",
                    Weight = 42,
                    Values =
                    [
                        new PrioritisationCriterionValueDto { Id = ValueId, Value = "Low", Score = 10 }
                    ]
                }
            ]
        }
    ];

    [Fact]
    public async Task Get_RendersSelectedCategoryCriteriaAndCriterionValueScores()
    {
        using var factory = CreateFactory(new FakePrioritisationVariablesApiService(Categories));
        var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/CrossProfileAdmin/PrioritisationVariables?SelectedCategoryId={CategoryId}&SelectedCriterionId={CriterionId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("DI1 - Herd prevalence", body);
        Assert.Contains("Low", body);
    }

    private static WebApplicationFactory<Program> CreateFactory(IPrioritisationVariablesApiService fakeService) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPrioritisationVariablesApiService>();
                services.AddSingleton(fakeService);
            }));
}
