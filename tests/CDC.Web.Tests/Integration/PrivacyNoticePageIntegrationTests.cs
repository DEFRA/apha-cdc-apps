using System.Net;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc.Testing;

namespace CDC.Web.Tests.Integration;

public class PrivacyNoticePageIntegrationTests : IClassFixture<CdcWebTestFactory>
{
    private readonly CdcWebTestFactory _factory;

    public PrivacyNoticePageIntegrationTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ASignedOutVisitor_CanReadThePrivacyNotice()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var response = await client.GetAsync("/privacy-notice");
        var page = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<title>Privacy notice", page);
        Assert.Contains("<h1 class=\"govuk-heading-l\">Privacy notice for Disease Briefing, Decisions Support, Risk Assessment &amp; Ranking</h1>", page);
    }

    // The sections the GOV.UK Service Manual says a privacy notice must explain.
    [Theory]
    [InlineData("Who we are and who controls your data")]
    [InlineData("What personal information we collect")]
    [InlineData("Why we collect it and our legal basis")]
    [InlineData("What we do with it")]
    [InlineData("Who we share it with")]
    [InlineData("How long we keep it")]
    [InlineData("Transfers outside the UK")]
    [InlineData("Automated decisions")]
    [InlineData("Your rights")]
    [InlineData("Contact and complaints")]
    public async Task ThePrivacyNotice_HasTheRequiredSection(string heading)
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var page = await client.GetStringAsync("/privacy-notice");

        Assert.Contains($"<h2 class=\"govuk-heading-m\">{heading}</h2>", page);
    }

    [Fact]
    public async Task TheFooterPrivacyNoticeLink_GoesToThePrivacyNotice()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var page = await client.GetStringAsync("/");

        Assert.Contains("<a class=\"govuk-footer__link\" href=\"/privacy-notice\">Privacy notice</a>", page);
    }
}
