using System.Net;
using CDC.Web.Infrastructure;
using CDC.Web.Models;
using CDC.Web.Tests.Features.Landing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// The default WebApplicationFactory<Program> has no live CDC.Api, so this page only ever
// renders its "no reports to display" path. These tests supply real report data so the table,
// the items-per-page control and the pager actually render.
public class HelpSupportIntegrationTests
{
    [Fact]
    public async Task StaticReports_RendersTheReportTable_WithPublicAndEffectiveDateColumns()
    {
        using var factory = CreateFactory(Reports(3));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/HelpSupport/StaticReports");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Report 1", body);
        Assert.Contains("Public?", body);
        Assert.Contains("Items per page:", body);
    }

    [Fact]
    public async Task StaticReports_RendersThePager_WhenThereAreMoreReportsThanOnePage()
    {
        using var factory = CreateFactory(Reports(30));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/HelpSupport/StaticReports?PageNumber=2&PageSize=10");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("govuk-pagination", body);
        Assert.Contains("Previous", body);
        Assert.Contains("Next", body);
    }

    [Fact]
    public async Task StaticReports_OmitsThePublicColumn_ForUserManuals()
    {
        using var factory = CreateFactory(Reports(2, isUserManual: true));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/HelpSupport/StaticReports?UserManual=1");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Help Using D2R2", body);
        Assert.DoesNotContain("Public?", body);
    }

    [Fact]
    public async Task StaticReports_ShowsDeleteConfirmationAndAddButton_WhenPermitted()
    {
        using var factory = CreateFactory(Reports(1, isUserManual: true, canDelete: true), canUpload: true);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/HelpSupport/StaticReports?UserManual=1");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Are you sure you want to delete this user manual?", body);
        Assert.Contains(">Add<", body);
        Assert.Contains("History", body);
    }

    [Fact]
    public async Task StaticReports_HidesDeleteAndAdd_WhenNotPermitted()
    {
        using var factory = CreateFactory(Reports(1, isUserManual: true, canDelete: false), canUpload: false);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/HelpSupport/StaticReports?UserManual=1");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("Are you sure you want to delete this user manual?", body);
        Assert.DoesNotContain(">Add<", body);
    }

    [Fact]
    public async Task StaticReports_HistoryMode_RendersPreviousVersionsAndBreadcrumb()
    {
        var staticReportId = Guid.NewGuid();
        var history = new[]
        {
            new StaticReportListItemDto
            {
                Id = Guid.NewGuid(),
                StaticReportId = staticReportId,
                Title = "Help using D2R2",
                VersionMajor = 2,
                EffectiveDateFrom = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsUserManual = true,
                IsPublic = false,
                FileSize = 1024
            },
            new StaticReportListItemDto
            {
                Id = Guid.NewGuid(),
                StaticReportId = staticReportId,
                Title = "Help using D2R2",
                VersionMajor = 1,
                EffectiveDateFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                EffectiveDateTo = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsUserManual = true,
                IsPublic = false,
                FileSize = 1024
            }
        };

        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new Features.Landing.FakeApiClient(staticReportHistory: history));
            }));
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/HelpSupport/StaticReports?StaticReportId={staticReportId}&UserManual=1");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("History for Help using D2R2", body);
        Assert.Contains("Help Using D2R2", body); // parent breadcrumb link text
        Assert.DoesNotContain("Are you sure you want to delete", body); // no delete in history mode
    }

    private static IReadOnlyList<StaticReportListItemDto> Reports(int count, bool isUserManual = false, bool canDelete = false) =>
        [.. Enumerable.Range(1, count).Select(index => new StaticReportListItemDto
        {
            Id = Guid.NewGuid(),
            StaticReportId = Guid.NewGuid(),
            Title = $"Report {index}",
            VersionMajor = 1,
            EffectiveDateFrom = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            // Alternating so both the open-ended and closed effective-date formats render.
            EffectiveDateTo = index % 2 == 0 ? new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc) : null,
            IsUserManual = isUserManual,
            IsPublic = index % 2 == 0,
            FileSize = 1024,
            CanDelete = canDelete
        })];

    private static WebApplicationFactory<Program> CreateFactory(IReadOnlyList<StaticReportListItemDto> reports, bool canUpload = true) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new Features.Landing.FakeApiClient(staticReports: reports, canUploadStaticReports: canUpload));
            }));
}
