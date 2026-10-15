using System.Net;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Infrastructure.Pdf;
using CDC.Web.Models;
using CDC.Web.Tests.Infrastructure;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CDC.Web.Tests.Integration;

// Renders the real print page and the real profile report Razor view; only the CDC.Api clients and the
// Chromium PDF step are faked (the latter captures the HTML it was asked to print).
public class ProfilePrintVersionIntegrationTests
{
    private static readonly Guid ProfileId = Guid.NewGuid();
    private static readonly byte[] PdfBytes = "%PDF-1.7 print"u8.ToArray();

    [Fact]
    public async Task PrintFullProfile_DownloadsThePdfOfTheCurrentDraft_WithTheLegacyFileNameAndTitle()
    {
        var content = CreateContent();
        var pdf = new FakePdfService(PdfBytes);
        using var factory = CreateFactory(CreateProfile(), content, pdf);
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/ViewProfilePrintVersion/{ProfileId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PdfBytes, await response.Content.ReadAsByteArrayAsync());
        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName;
        Assert.Matches(@"Anthrax\(Draft\)_18\.7_Print full profile_\d{2}_\d{2}_\d{4}_\d{2}_\d{2}_\d{2}\.pdf", fileName);

        var html = pdf.Html!;
        Assert.Contains("<title>Full profile for Anthrax - v18.7 (Draft) Valid from 05/08/2026 to 16/08/2026</title>", html);
        Assert.Contains("Full profile for: Anthrax", html);
        Assert.Contains("<p>Version: 18.7</p>", html);
        Assert.Contains("DRAFT CONTENT", html);
        Assert.Contains("<a href=\"#section-1\">1. Description</a>", html);
        Assert.Contains("<a href=\"#section-2\">2. Geography</a>", html);
        Assert.Contains("<a href=\"#references\">3. Sources of further information and references</a>", html);
        Assert.Contains("<h3 class=\"rpt-references-section\">2. Geography</h3>", html);
        Assert.Contains("<h2 class=\"rpt-section-heading\">Please Note</h2>", html);
    }

    [Fact]
    public async Task PrintSection_DownloadsJustThatSectionWithoutAFrontPage_AndItsReferenceTables()
    {
        var content = CreateContent();
        var pdf = new FakePdfService(PdfBytes);
        using var factory = CreateFactory(CreateProfile(), content, pdf);
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/ViewProfilePrintVersion/{ProfileId}?section={content.Sections[1].Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);

        var html = pdf.Html!;
        Assert.DoesNotContain("<section class=\"rpt-front\">", html);
        Assert.DoesNotContain("1. Description", html);
        Assert.Contains("<h2 class=\"rpt-section-heading\">2. Geography</h2>", html);
        Assert.Contains("<h4 class=\"rpt-references-title\">Scientific paper reference</h4>", html);
        Assert.DoesNotContain("Sources of further information and references", html);
    }

    [Fact]
    public async Task PrintFullProfile_Returns403_WhenTheUserMayNotPrintTheCurrentVersion()
    {
        var pdf = new FakePdfService(PdfBytes);
        using var factory = CreateFactory(CreateProfile() with { CanPrintProfileSection = false }, CreateContent(), pdf);
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/ViewProfilePrintVersion/{ProfileId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(pdf.Html);
    }

    [Fact]
    public async Task PrintFullProfile_ReturnsNotFound_ForAnUnknownProfile()
    {
        using var factory = CreateFactory(profile: null, CreateContent(), new FakePdfService(PdfBytes));
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/ViewProfilePrintVersion/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PrintSection_ReturnsBadRequest_WhenTheSectionIsNotAGuid_InsteadOfPrintingTheWholeProfile()
    {
        var pdf = new FakePdfService(PdfBytes);
        using var factory = CreateFactory(CreateProfile(), CreateContent(), pdf);
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/ViewProfilePrintVersion/{ProfileId}?section=not-a-guid");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(pdf.Html);
    }

    [Fact]
    public async Task PrintFullProfile_ShowsTheErrorPage_WhenThePdfCannotBeRendered()
    {
        using var factory = CreateFactory(CreateProfile(), CreateContent(), new FakePdfService(PdfBytes, new PdfRenderException("no browser")));
        var client = await factory.SignedInClientAsync();

        var response = await client.GetAsync($"/SurveillanceProfiles/ViewProfilePrintVersion/{ProfileId}");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<title>Error: Print full profile", body);
        Assert.Contains("There is a problem", body);
        Assert.Contains("We could not create the print version of this profile", body);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Print full profile</h1>", body);
        Assert.Contains($"href=\"/SurveillanceProfiles/EditProfileQuestions/{ProfileId}\"", body);
    }

    private static ManageProfileViewModel CreateProfile() => new()
    {
        ProfileId = ProfileId,
        ProfileTitle = "Anthrax",
        CurrentProfileVersionId = Guid.NewGuid(),
        CurrentVersionNumber = "18.7",
        CurrentVersionValidFrom = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc),
        CurrentVersionValidTo = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc),
        CanPrintProfileSection = true
    };

    private static ProfileReportContentDto CreateContent() => new()
    {
        ProfileVersionId = Guid.NewGuid(),
        ProfileId = ProfileId,
        FullTitle = "Anthrax",
        VersionLabel = "18.7",
        EffectiveDateFrom = new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc),
        EffectiveDateTo = new DateTime(2026, 8, 16, 0, 0, 0, DateTimeKind.Utc),
        ShowDraftWatermark = true,
        Sections =
        [
            ReportContent.Section(1, "Description", questions: ReportContent.Answered(1, "What is it?", "A disease")),
            ReportContent.Section(2, "Geography", questions: ReportContent.Answered(1, "Where is it?", "Everywhere"))
        ]
    };

    private static WebApplicationFactory<Program> CreateFactory(ManageProfileViewModel? profile, ProfileReportContentDto content, IPlaywrightPdfService pdf) =>
        new CdcWebTestFactory().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: profile));
                services.RemoveAll<IProfileReportsApiService>();
                services.AddSingleton<IProfileReportsApiService>(new FakeProfileReportsApiService(content: new ProfileReportContentResult(ProfileReportsOutcome.Success, content)));
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(new FakeProfileSectionsApiService(noteTypes: []));
                services.RemoveAll<IPlaywrightPdfService>();
                services.AddSingleton(pdf);
            }));
}
