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

    private static WebApplicationFactory<Program> CreateFactory(ISpeciesApiService fakeService) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<ISpeciesApiService>();
                services.AddSingleton(fakeService);
            }));
}
