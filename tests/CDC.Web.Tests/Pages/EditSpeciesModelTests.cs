using CDC.Web.Models;
using CDC.Web.Pages;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;

namespace CDC.Web.Tests.Pages;

public class EditSpeciesModelTests
{
    private static readonly Guid SpeciesId = Guid.NewGuid();

    private static EditSpeciesModel CreatePageModel(
        SpeciesDetailDto? speciesDetail = null,
        Exception? throwOnGetSpeciesDetail = null,
        string? section = null)
    {
        var modelMetadataProvider = new EmptyModelMetadataProvider();
        var pageModel = new EditSpeciesModel(
            new FakeSpeciesApiService(speciesDetail: speciesDetail, throwOnGetSpeciesDetail: throwOnGetSpeciesDetail),
            NullLogger<EditSpeciesModel>.Instance)
        {
            SpeciesId = SpeciesId,
            Section = section,
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext(),
                ViewData = new ViewDataDictionary(modelMetadataProvider, new ModelStateDictionary())
            },
            MetadataProvider = modelMetadataProvider
        };

        return pageModel;
    }

    private static SpeciesDetailDto Species() => new() { Id = SpeciesId, Name = "Ruminants", ParentId = Guid.Empty };

    [Fact]
    public async Task OnGetAsync_PopulatesSpeciesName_WhenTheSpeciesExists()
    {
        var pageModel = CreatePageModel(Species());

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.Equal("Ruminants", pageModel.SpeciesName);
        Assert.False(pageModel.HasError);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsNotFound_WhenTheSpeciesDoesNotExist()
    {
        var pageModel = CreatePageModel(speciesDetail: null);

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Null(pageModel.SpeciesName);
    }

    [Fact]
    public async Task OnGetAsync_ReturnsPageWithHasError_WhenLoadingTheSpeciesFails()
    {
        var pageModel = CreatePageModel(throwOnGetSpeciesDetail: new HttpRequestException("connection refused"));

        var result = await pageModel.OnGetAsync(CancellationToken.None);

        Assert.IsType<PageResult>(result);
        Assert.True(pageModel.HasError);
        Assert.Null(pageModel.SpeciesName);
    }

    [Fact]
    public async Task OnGetAsync_DefaultsToTheFirstSection_WhenNoSectionIsRequested()
    {
        var pageModel = CreatePageModel(Species());

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(EditSpeciesSectionCatalog.Sections[0].Key, pageModel.CurrentSection.Key);
        Assert.Null(pageModel.PreviousSection);
        Assert.Equal(EditSpeciesSectionCatalog.Sections[1].Key, pageModel.NextSection!.Key);
    }

    [Fact]
    public async Task OnGetAsync_SelectsTheRequestedSection_AndComputesItsNeighbours()
    {
        var pageModel = CreatePageModel(Species(), section: "movements");

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal("movements", pageModel.CurrentSection.Key);
        Assert.Equal(EditSpeciesSectionCatalog.Sections[0].Key, pageModel.PreviousSection!.Key);
        Assert.Equal(EditSpeciesSectionCatalog.Sections[2].Key, pageModel.NextSection!.Key);
    }

    [Fact]
    public async Task OnGetAsync_FallsBackToTheFirstSection_WhenAnUnknownSectionIsRequested()
    {
        var pageModel = CreatePageModel(Species(), section: "not-a-real-section");

        await pageModel.OnGetAsync(CancellationToken.None);

        Assert.Equal(EditSpeciesSectionCatalog.Sections[0].Key, pageModel.CurrentSection.Key);
    }
}
