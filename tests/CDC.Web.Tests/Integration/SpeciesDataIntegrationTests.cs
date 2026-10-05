using System.Net;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Pages;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// The default WebApplicationFactory<Program> has no live CDC.Api to call, so every species page
// only ever renders its error-banner path. These tests swap in a fake ISpeciesApiService with
// real data, so the tree/audit-trail/edit-panel views actually render their success paths.
public class SpeciesDataIntegrationTests
{
    private static readonly Guid CattleId = Guid.Parse("6d0b9f0e-6d0f-4a1a-9a1e-2b1f2c3d4e5f");
    private static readonly Guid DairyId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly IReadOnlyList<SpeciesDto> Species =
    [
        new SpeciesDto { Id = CattleId, ParentId = Guid.Empty, Description = "Cattle", IsActive = true, IsInUse = true },
        new SpeciesDto { Id = DairyId, ParentId = CattleId, Description = "Dairy cattle", IsActive = true, IsInUse = true }
    ];

    [Fact]
    public async Task Maintain_RendersSpeciesTree_WhenSpeciesAreAvailable()
    {
        using var factory = CreateFactory(new FakeSpeciesApiService(Species));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/SpeciesData/Maintain");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Cattle", body);
        Assert.Contains("app-tree", body);
    }

    [Fact]
    public async Task Maintain_RendersAuditTrail_WhenHandlerRequested()
    {
        IReadOnlyList<SpeciesAuditTrailEntryDto> auditTrail =
        [
            new SpeciesAuditTrailEntryDto
            {
                Id = Guid.NewGuid(),
                OldName = "Dairy cattle",
                NewName = "Dairy",
                OldParent = "Cattle",
                NewParent = "Cattle",
                ChangedBy = "a.user",
                LogDate = DateTime.UtcNow,
                ReasonForChange = "Simplifying the name"
            }
        ];
        using var factory = CreateFactory(new FakeSpeciesApiService(Species, auditTrail: auditTrail));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/SpeciesData/Maintain?handler=AuditTrail");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Species name/parent audit trail", body);
        Assert.Contains("Simplifying the name", body);
    }

    [Fact]
    public async Task ViewSpeciesData_RendersSpeciesTree_WhenSpeciesAreAvailable()
    {
        using var factory = CreateFactory(new FakeSpeciesApiService(Species));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/ViewSpeciesData");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Cattle", body);
    }

    [Fact]
    public async Task Maintain_RendersEditPanel_WhenSpeciesSelectedForEditing()
    {
        var speciesDetail = new SpeciesDetailDto
        {
            Id = DairyId,
            Name = "Dairy cattle",
            ParentId = CattleId,
            ParentName = "Cattle",
            IsActive = true,
            IsInUse = true,
            LastUpdated = [1, 2, 3, 4]
        };
        IReadOnlyList<SpeciesValidParentDto> validParents =
        [
            new SpeciesValidParentDto { Id = CattleId, Name = "Cattle" }
        ];
        var fakeService = new FakeSpeciesApiService(Species, speciesDetail: speciesDetail, validParents: validParents);
        using var factory = CreateFactory(fakeService);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var initialResponse = await client.GetAsync("/SpeciesData/Maintain");
        var initialHtml = await initialResponse.Content.ReadAsStringAsync();
        var tokenMatch = System.Text.RegularExpressions.Regex.Match(
            initialHtml,
            "<input[^>]*name=\\\"__RequestVerificationToken\\\"[^>]*value=\\\"([^\\\"]+)\\\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        Assert.True(tokenMatch.Success, "Expected anti-forgery token in the page markup.");

        var editResponse = await client.PostAsync(
            "/SpeciesData/Maintain?handler=EditNameParent",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("species", DairyId.ToString()),
                new KeyValuePair<string, string>("__RequestVerificationToken", tokenMatch.Groups[1].Value)
            ]));

        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);

        var editHtml = await editResponse.Content.ReadAsStringAsync();

        Assert.Contains("Dairy cattle", editHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reason for change", editHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Maintain_RendersSelectionError_WhenNoSpeciesSelectedForEditing()
    {
        var fakeService = new FakeSpeciesApiService(Species);
        using var factory = CreateFactory(fakeService);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var initialResponse = await client.GetAsync("/SpeciesData/Maintain");
        var initialHtml = await initialResponse.Content.ReadAsStringAsync();
        var tokenMatch = System.Text.RegularExpressions.Regex.Match(
            initialHtml,
            "<input[^>]*name=\\\"__RequestVerificationToken\\\"[^>]*value=\\\"([^\\\"]+)\\\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        Assert.True(tokenMatch.Success, "Expected anti-forgery token in the page markup.");

        var editResponse = await client.PostAsync(
            "/SpeciesData/Maintain?handler=EditNameParent",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", tokenMatch.Groups[1].Value)
            ]));

        var editHtml = await editResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);
        Assert.Contains("Select a species or species group to edit.", editHtml, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Maintain_RendersValidationErrorSummary_WhenSaveSubmittedWithoutRequiredFields()
    {
        var speciesDetail = new SpeciesDetailDto
        {
            Id = DairyId,
            Name = "Dairy cattle",
            ParentId = CattleId,
            ParentName = "Cattle",
            IsActive = true,
            IsInUse = true,
            LastUpdated = [1, 2, 3, 4]
        };
        var fakeService = new FakeSpeciesApiService(Species, speciesDetail: speciesDetail);
        using var factory = CreateFactory(fakeService);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var initialResponse = await client.GetAsync("/SpeciesData/Maintain");
        var initialHtml = await initialResponse.Content.ReadAsStringAsync();
        var tokenMatch = System.Text.RegularExpressions.Regex.Match(
            initialHtml,
            "<input[^>]*name=\\\"__RequestVerificationToken\\\"[^>]*value=\\\"([^\\\"]+)\\\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        Assert.True(tokenMatch.Success, "Expected anti-forgery token in the page markup.");

        var saveResponse = await client.PostAsync(
            "/SpeciesData/Maintain?handler=Save",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("Input.SpeciesId", DairyId.ToString()),
                new KeyValuePair<string, string>("Input.Name", string.Empty),
                new KeyValuePair<string, string>("Input.Reason", string.Empty),
                new KeyValuePair<string, string>("Input.LastUpdatedBase64", Convert.ToBase64String(speciesDetail.LastUpdated)),
                new KeyValuePair<string, string>("__RequestVerificationToken", tokenMatch.Groups[1].Value)
            ]));

        var saveHtml = await saveResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        Assert.Contains("There is a problem", saveHtml, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("You need to provide a new name for this species.", saveHtml, StringComparison.OrdinalIgnoreCase);
    }

    private static WebApplicationFactory<Program> CreateFactory(ISpeciesApiService fakeService) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISpeciesApiService>();
                services.AddSingleton(fakeService);
            }));
}
