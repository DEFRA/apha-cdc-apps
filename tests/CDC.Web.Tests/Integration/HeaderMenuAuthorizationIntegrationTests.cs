using System.Net;
using System.Text.RegularExpressions;
using CDC.Web.Authorization.Constants;
using CDC.Web.Models;
using CDC.Web.Tests.TestSupport;

namespace CDC.Web.Tests.Integration;

// Renders a real page through the whole pipeline (sign-in cookie, claims transformation, policies, Razor) so the
// header "Menu" is shown to be listing exactly the links the signed-in user's role allows.
public partial class HeaderMenuAuthorizationIntegrationTests : IClassFixture<CdcWebTestFactory>
{
    // The home page: open to every signed-in user and needs no CDC.Api call to render the layout.
    private const string StaticPage = "/Landing/Internal";

    private readonly CdcWebTestFactory _factory;

    public HeaderMenuAuthorizationIntegrationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProfileEditorRole_ListsEveryMenuLink()
    {
        var client = await _factory.SignedInClientWithRolesAsync(AuthorizationRoles.ProfileEditor);

        var menu = await GetMenuAsync(client);

        Assert.Equal(HeaderMenu.Sections.Select(section => section.Title), menu.Sections);
        Assert.Equal(HeaderMenu.Sections.SelectMany(section => section.Links).Select(link => link.Text), menu.Links);
    }

    // User Admin and Cross Profile Admin sit in the footer only, as in the legacy site - see FooterNavigationIntegrationTests.
    [Fact]
    public async Task ProfileEditorRole_ListsOnlyTheThreeHeaderSections_WithNoUserAdminOrCrossProfileAdminLinks()
    {
        var client = await _factory.SignedInClientWithRolesAsync(AuthorizationRoles.ProfileEditor);

        var menu = await GetMenuAsync(client);

        Assert.Equal(["Disease Profiles", "Reports", "Species Data"], menu.Sections);
        Assert.Equal(
            [
                "Search disease profiles", "Create profile", "Compare profile versions", "Review Timings",
                "General reports", "Questions and guidance reports", "Disease ranking report",
                "View species data", "Maintain species data"
            ],
            menu.Links);
    }

    [Fact]
    public async Task ProfileEditorFlagFromTheUserRecord_ListsEveryMenuLink()
    {
        var client = await _factory.SignedInClientWithPermissionsAsync(isProfileEditor: true, isPolicyProfileUser: false);

        var menu = await GetMenuAsync(client);

        Assert.Equal(HeaderMenu.Sections.Select(section => section.Title), menu.Sections);
        Assert.Equal(HeaderMenu.Sections.SelectMany(section => section.Links).Select(link => link.Text), menu.Links);
    }

    [Fact]
    public async Task PolicyProfileUserFlagFromTheUserRecord_ListsOnlyTheLinksThatRoleMayUse()
    {
        var client = await _factory.SignedInClientWithPermissionsAsync(isProfileEditor: false, isPolicyProfileUser: true);

        var menu = await GetMenuAsync(client);

        Assert.Equal(["Disease Profiles", "Reports", "Species Data"], menu.Sections);
        Assert.Equal(["Search disease profiles", "General reports", "Disease ranking report", "View species data"], menu.Links);
    }

    [Fact]
    public async Task UserWithNoPermissionsOnTheirRecord_ListsNoMenuLinks()
    {
        var client = await _factory.SignedInClientWithPermissionsAsync(isProfileEditor: false, isPolicyProfileUser: false);

        var menu = await GetMenuAsync(client);

        Assert.Empty(menu.Sections);
        Assert.Empty(menu.Links);
    }

    private static async Task<(List<string> Sections, List<string> Links)> GetMenuAsync(HttpClient client)
    {
        var page = await client.GetStringAsync(StaticPage);

        return (
            SectionHeadingRegex().Matches(page).Select(match => WebUtility.HtmlDecode(match.Groups[1].Value)).ToList(),
            LinkRegex().Matches(page).Select(match => WebUtility.HtmlDecode(match.Groups[1].Value)).ToList());
    }

    [GeneratedRegex("""<h2 class="govuk-heading-s app-menu-panel__heading">([^<]+)</h2>""")]
    private static partial Regex SectionHeadingRegex();

    [GeneratedRegex("""<a class="govuk-link govuk-link--no-visited-state app-menu-panel__link[^"]*"[^>]*>([^<]+)</a>""")]
    private static partial Regex LinkRegex();
}
