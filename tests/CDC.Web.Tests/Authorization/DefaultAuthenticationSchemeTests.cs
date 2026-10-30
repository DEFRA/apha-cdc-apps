using CDC.Web.Authorization;
using CDC.Web.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace CDC.Web.Tests.Authorization;

// Whichever identity provider registers last, each request is authenticated with the session cookie it carries.
public class DefaultAuthenticationSchemeTests : IClassFixture<CdcWebTestFactory>
{
    private readonly CdcWebTestFactory _factory;

    public DefaultAuthenticationSchemeTests(CdcWebTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EveryDefaultScheme_IsTheOneThatPicksTheCookieTheRequestCarries()
    {
        var schemes = _factory.Services.GetRequiredService<IAuthenticationSchemeProvider>();

        Assert.Equal(InternalOrExternalAuthenticationScheme.Name, (await schemes.GetDefaultAuthenticateSchemeAsync())?.Name);
        Assert.Equal(InternalOrExternalAuthenticationScheme.Name, (await schemes.GetDefaultChallengeSchemeAsync())?.Name);
        Assert.Equal(InternalOrExternalAuthenticationScheme.Name, (await schemes.GetDefaultForbidSchemeAsync())?.Name);
    }
}
