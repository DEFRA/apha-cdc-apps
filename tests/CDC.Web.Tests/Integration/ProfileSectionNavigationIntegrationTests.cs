using System.Net;
using System.Text.RegularExpressions;
using CDC.Common.Contracts;
using CDC.Web.Infrastructure;
using CDC.Web.Tests.Pages.SurveillanceProfiles;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using static CDC.Web.Tests.Pages.SurveillanceProfiles.ProfileTabTestData;

namespace CDC.Web.Tests.Integration;

// The left menu and the previous and next links of a profile page name each section as the legacy page did: its number, then its short name with
// only the first letter capitalised and " and " shown as " & " (FormattedShortName), not the section's long name.
public partial class ProfileSectionNavigationIntegrationTests : IClassFixture<CdcWebTestFactory>
{
    private static readonly Guid WiderSocietyId = Guid.NewGuid();
    private static readonly Guid LegislationId = Guid.NewGuid();
    private static readonly Guid ApproachToControlId = Guid.NewGuid();

    // The names the database holds for sections 6 to 9, 12 and 15.
    private static readonly (int Number, Guid Id, string Name, string ShortName)[] Sections =
    [
        (6, Guid.NewGuid(), "Human health implications", "Human Health"),
        (7, WiderSocietyId, "Impact on wider society and stakeholder groups", "Wider Society"),
        (8, LegislationId, "Legislation and other mandates", "Legislation And Mandates"),
        (9, ApproachToControlId, "Approach to control of this issue in animals in the UK", "Approach To Control"),
        (12, Guid.NewGuid(), "Current surveillance activities in the UK", "Surveillance"),
        (15, Guid.NewGuid(), "Public and stakeholder perception", "Public And Stakeholder Perception")
    ];

    private readonly CdcWebTestFactory _factory;

    public ProfileSectionNavigationIntegrationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [GeneratedRegex("""<nav class="app-subnav" aria-label="Profile reference sections">(.*?)</nav>""", RegexOptions.Singleline)]
    private static partial Regex LeftMenuRegex();

    [GeneratedRegex("""(\d+)\. ([^<\r\n]+?)\s*</a>""")]
    private static partial Regex LeftMenuLinkRegex();

    [GeneratedRegex("""govuk-pagination__link-label">([^<]+)</span>""")]
    private static partial Regex PaginationLabelRegex();

    private static ProfileQuestionnaireMetadataDto Metadata() => new()
    {
        Sections =
        [
            .. Sections.Select(section => new ProfileSectionMetadataDto
            {
                Id = section.Id,
                Name = section.Name,
                ShortName = section.ShortName,
                SectionNumber = section.Number,
                Questions = []
            })
        ]
    };

    private WebApplicationFactory<Program> CreateFactory() =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IApiClient>();
                services.AddSingleton<IApiClient>(new CDC.Web.Tests.Features.Landing.FakeApiClient(manageProfile: DraftProfile()));
                services.RemoveAll<IProfileSectionsApiService>();
                services.AddSingleton<IProfileSectionsApiService>(new FakeProfileSectionsApiService(metadata: Metadata()));
            }));

    private async Task<string> GetAsync(string path)
    {
        using var factory = CreateFactory();
        var client = await factory.SignedInClientAsync();
        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return await response.Content.ReadAsStringAsync();
    }

    private static List<string> LeftMenuTitles(string body)
    {
        var menu = LeftMenuRegex().Match(body);
        Assert.True(menu.Success, "The page should have the left menu of sections.");

        return [.. LeftMenuLinkRegex().Matches(menu.Groups[1].Value).Select(link => WebUtility.HtmlDecode($"{link.Groups[1].Value}. {link.Groups[2].Value}"))];
    }

    [Fact]
    public async Task LeftMenu_NamesEachSectionByItsFormattedShortName_OnTheQuestionsTab()
    {
        var body = await GetAsync($"/SurveillanceProfiles/EditProfileQuestions/{ProfileId}");

        Assert.Equal(
            [
                "6. Human health",
                "7. Wider society",
                "8. Legislation & mandates",
                "9. Approach to control",
                "12. Surveillance",
                "15. Public & stakeholder perception"
            ],
            LeftMenuTitles(body));
    }

    [Fact]
    public async Task LeftMenu_NamesEachSectionByItsFormattedShortName_OnTheOtherTabsAndThePageOfReferences()
    {
        foreach (var path in new[] { "EditProfileReferences", "EditProfileFurtherInformation", "Notes" })
        {
            var body = await GetAsync($"/SurveillanceProfiles/{path}/{ProfileId}");

            Assert.Equal("6. Human health", LeftMenuTitles(body)[0]);
            Assert.DoesNotContain("Human health implications</a>", body);
        }
    }

    [Fact]
    public async Task LeftMenu_DoesNotShowTheSectionsLongNames()
    {
        var body = await GetAsync($"/SurveillanceProfiles/EditProfileQuestions/{ProfileId}");
        var menu = LeftMenuRegex().Match(body).Groups[1].Value;

        Assert.DoesNotContain("Human health implications", menu);
        Assert.DoesNotContain("Impact on wider society", menu);
        Assert.DoesNotContain("Legislation and other mandates", menu);
        Assert.DoesNotContain("Approach to control of this issue", menu);
        Assert.DoesNotContain("Current surveillance activities", menu);
        Assert.DoesNotContain("Public and stakeholder perception", menu);
    }

    [Fact]
    public async Task PreviousAndNextLinks_NameTheNeighbouringSectionsByTheirFormattedShortNames()
    {
        var body = await GetAsync($"/SurveillanceProfiles/EditProfileQuestions/{ProfileId}?section={LegislationId}");

        var labels = PaginationLabelRegex().Matches(body).Select(label => WebUtility.HtmlDecode(label.Groups[1].Value)).ToList();

        Assert.Equal(["7. Wider society", "9. Approach to control"], labels);
    }
}
