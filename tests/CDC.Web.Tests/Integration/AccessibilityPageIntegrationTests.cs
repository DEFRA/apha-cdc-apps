using System.Net;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CDC.Web.Tests.Integration;

public class AccessibilityPageIntegrationTests : IClassFixture<CdcWebTestFactory>
{
    private readonly CdcWebTestFactory _factory;

    public AccessibilityPageIntegrationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ASignedOutVisitor_CanReadTheAccessibilityStatement()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var response = await client.GetAsync("/accessibility");
        var page = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<title>Accessibility statement", page);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Accessibility statement for Disease Briefing, Decisions Support, Risk Assessment &amp; Ranking</h1>", page);
        Assert.Contains("href=\"mailto:D2R2@defra.gov.uk\"", page);
    }

    // The sections the accessibility regulations' model statement requires.
    [Theory]
    [InlineData("<h2 class=\"govuk-heading-m\">How accessible this website is</h2>")]
    [InlineData("<h2 class=\"govuk-heading-m\">Feedback and contact information</h2>")]
    [InlineData("<h2 class=\"govuk-heading-m\">Reporting accessibility problems with this website</h2>")]
    [InlineData("<h2 class=\"govuk-heading-m\">Enforcement procedure</h2>")]
    [InlineData("<h3 class=\"govuk-heading-s\">Compliance status</h3>")]
    [InlineData("<h2 class=\"govuk-heading-m\">Preparation of this accessibility statement</h2>")]
    public async Task TheAccessibilityStatement_HasTheRequiredSection(string heading)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var page = await client.GetStringAsync("/accessibility");

        Assert.Contains(heading, page);
    }

    [Fact]
    public async Task TheFooterAccessibilityLink_GoesToTheAccessibilityStatement()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var page = await client.GetStringAsync("/");

        Assert.Contains("<a class=\"govuk-footer__link\" href=\"/accessibility\">Accessibility statement</a>", page);
    }
}
