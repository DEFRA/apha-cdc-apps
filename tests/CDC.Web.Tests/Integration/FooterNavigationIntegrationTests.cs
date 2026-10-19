using System.Net;
using System.Text.RegularExpressions;
using CDC.Web.Authorization.Constants;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CDC.Web.Tests.Integration;

// Renders a real page through the whole pipeline (sign-in cookie, claims transformation, policies, Razor) so the footer
// is shown to list exactly the groups and links the legacy Footer.ascx showed the signed-in user's role: User Admin and
// Cross Profile Admin live here and not in the header, and a group with nothing visible is omitted.
public partial class FooterNavigationIntegrationTests : IClassFixture<CdcWebTestFactory>
{
    // The home page: open to every signed-in user and needs no CDC.Api call to render the layout.
    private const string StaticPage = "/Landing/Internal";

    private readonly CdcWebTestFactory _factory;

    public FooterNavigationIntegrationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProfileEditor_SeesEveryFooterGroupAndLink()
    {
        var client = await _factory.SignedInClientWithRolesAsync(AuthorizationRoles.ProfileEditor);

        var footer = await GetFooterAsync(client, StaticPage);

        Assert.Equal(["User Admin", "Cross Profile Admin", "Help and Support"], footer.Groups);
        Assert.Equal(
            [
                ("Global users", "/UserAdmin/GlobalUsers"),
                ("External users", "/UserAdmin/ExternalUsers"),
                ("Maintain reference data", "/CrossProfileAdmin/ReferenceData"),
                ("Maintain prioritisation variables", "/CrossProfileAdmin/PrioritisationVariables"),
                ("Maintain cross-cutting issue scores", "/CrossProfileAdmin/CrossCuttingIssueScores"),
                ("Help using D2R2", "/HelpSupport/StaticReports?UserManual=1"),
                ("D2R2 Quality Statement", "/HelpSupport/QualityStatement")
            ],
            footer.Links);
    }

    [Fact]
    public async Task ProfileEditorFlagFromTheUserRecord_SeesEveryFooterGroup()
    {
        var client = await _factory.SignedInClientWithPermissionsAsync(isProfileEditor: true, isPolicyProfileUser: false);

        var footer = await GetFooterAsync(client, StaticPage);

        Assert.Equal(["User Admin", "Cross Profile Admin", "Help and Support"], footer.Groups);
    }

    [Fact]
    public async Task PolicyProfileUser_SeesOnlyHelpAndSupport()
    {
        var client = await _factory.SignedInClientWithPermissionsAsync(isProfileEditor: false, isPolicyProfileUser: true);

        var footer = await GetFooterAsync(client, StaticPage);

        Assert.Equal(["Help and Support"], footer.Groups);
        Assert.Equal(
            [("Help using D2R2", "/HelpSupport/StaticReports?UserManual=1"), ("D2R2 Quality Statement", "/HelpSupport/QualityStatement")],
            footer.Links);
    }

    [Fact]
    public async Task UserWithNoPermissions_SeesNoFooterNavigation_ButKeepsTheStandardFooterLinks()
    {
        var client = await _factory.SignedInClientWithPermissionsAsync(isProfileEditor: false, isPolicyProfileUser: false);

        var footer = await GetFooterAsync(client, StaticPage);

        Assert.Empty(footer.Groups);
        Assert.Empty(footer.Links);
        Assert.DoesNotContain("govuk-footer__navigation", footer.Page);
        Assert.DoesNotContain("govuk-footer__section-break", footer.Page);
        Assert.Contains("Back to top", footer.Page);
        Assert.Contains("Open Government Licence", footer.Page);
    }

    [Fact]
    public async Task SignedOutVisitor_SeesNoFooterNavigation()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var footer = await GetFooterAsync(client, "/Account/SignedOut");

        Assert.Empty(footer.Groups);
        Assert.Empty(footer.Links);
    }

    private static async Task<(string Page, List<string> Groups, List<(string Text, string Href)> Links)> GetFooterAsync(HttpClient client, string path)
    {
        var page = await client.GetStringAsync(path);

        return (
            page,
            GroupHeadingRegex().Matches(page).Select(match => WebUtility.HtmlDecode(match.Groups[1].Value)).ToList(),
            ListLinkRegex().Matches(page)
                .Select(match => (WebUtility.HtmlDecode(match.Groups[2].Value), WebUtility.HtmlDecode(match.Groups[1].Value)))
                .ToList());
    }

    [GeneratedRegex("""<h2 class="govuk-footer__heading govuk-heading-m">([^<]+)</h2>""")]
    private static partial Regex GroupHeadingRegex();

    [GeneratedRegex("""<li class="govuk-footer__list-item"><a class="govuk-footer__link" href="([^"]*)">([^<]+)</a></li>""")]
    private static partial Regex ListLinkRegex();
}
