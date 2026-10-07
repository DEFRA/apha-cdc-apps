using System.Security.Claims;
using CDC.Auth.Cidm;
using CDC.Auth.Entra;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace CDC.Web.Tests.TestSupport;

/// <summary>
/// Supplies dummy CIDM and Entra ID configuration (so options validation doesn't fail startup) and
/// static, network-free OpenID Connect discovery documents (so neither handler ever makes a real
/// HTTP call), so integration tests can exercise the auth pipeline without a real DEFRA CIDM tenant
/// or Entra ID tenant. Also adds a test-only sign-in endpoint so tests can reach authenticated-only
/// branches without simulating a full redirect/callback handshake.
/// </summary>
public sealed class CdcWebTestFactory : WebApplicationFactory<Program>
{
    public const string FakeAuthorizationEndpoint = "https://cidm.test/oauth2/v2.0/authorize";
    public const string FakeEndSessionEndpoint = "https://cidm.test/signout";
    public const string FakeEntraAuthorizationEndpoint = "https://entra.test/oauth2/v2.0/authorize";
    public const string TestSignInPath = "/__test/sign-in";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs reads Api:BaseUrl from builder.Configuration before builder.Build() runs, earlier
        // than ConfigureAppConfiguration below ever applies - UseSetting is the only config source that
        // lands in time. Api:BaseUrl now lives only in appsettings.Development.json, which is never
        // loaded unless ASPNETCORE_ENVIRONMENT=Development; CI runners don't set that env var (unlike
        // this machine, apparently), so the test host there defaults to Production and has no value
        // for it at all unless supplied here.
        builder.UseSetting("Api:BaseUrl", "http://cdc-api.test");

        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cidm:Address"] = "https://cidm.test/idphub/b2c",
            ["Cidm:Policy"] = "b2c_1a_test_signupsignin",
            ["Cidm:ClientId"] = "test-client-id",
            ["Cidm:ClientSecret"] = "test-client-secret",
            ["Cidm:ServiceId"] = "test-service-id",
            ["Entra:TenantId"] = "11111111-1111-1111-1111-111111111111",
            ["Entra:ClientId"] = "test-entra-client-id",
            ["Entra:ClientSecret"] = "test-entra-client-secret"
        }));

        builder.ConfigureServices(services =>
        {
            services.PostConfigure<OpenIdConnectOptions>(
                CidmAuthenticationDefaults.AuthenticationScheme,
                options =>
                {
                    var configuration = new OpenIdConnectConfiguration
                    {
                        Issuer = "https://cidm.test/",
                        AuthorizationEndpoint = FakeAuthorizationEndpoint,
                        TokenEndpoint = "https://cidm.test/oauth2/v2.0/token",
                        JwksUri = "https://cidm.test/discovery/v2.0/keys",
                        EndSessionEndpoint = FakeEndSessionEndpoint
                    };

                    // Explicitly overrides whatever ConfigurationManager the built-in
                    // OpenIdConnectPostConfigureOptions set up from MetadataAddress/Authority,
                    // guaranteeing no real HTTP discovery call happens in tests regardless of
                    // PostConfigure order.
                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                });

            services.PostConfigure<OpenIdConnectOptions>(
                EntraAuthenticationDefaults.AuthenticationScheme,
                options =>
                {
                    var configuration = new OpenIdConnectConfiguration
                    {
                        Issuer = "https://entra.test/",
                        AuthorizationEndpoint = FakeEntraAuthorizationEndpoint,
                        TokenEndpoint = "https://entra.test/oauth2/v2.0/token",
                        JwksUri = "https://entra.test/discovery/v2.0/keys",
                        EndSessionEndpoint = "https://entra.test/signout"
                    };

                    options.ConfigurationManager = new StaticConfigurationManager<OpenIdConnectConfiguration>(configuration);
                });

            services.AddSingleton<IStartupFilter, TestSignInStartupFilter>();
        });
    }

    // The app cookie may be Secure-only (CookieSecurePolicy.Always) - TestServer's default http://
    // client scheme means the CookieContainer silently drops it between requests unless every
    // request through this client is treated as HTTPS. This override alone is NOT sufficient:
    // WebApplicationFactory<T>.CreateClient(options) applies options.BaseAddress (default
    // http://localhost) AFTER calling ConfigureClient, silently reverting this back to http - every
    // caller must also request the client via CdcWebTestFactoryExtensions.SignedInClientAsync (or
    // pass WebApplicationFactoryClientOptions.BaseAddress explicitly) to actually get https.
    protected override void ConfigureClient(HttpClient client) => client.BaseAddress = new Uri("https://localhost");

    private sealed class TestSignInStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use(async (context, nextMiddleware) =>
            {
                if (context.Request.Path == TestSignInPath)
                {
                    var identity = new ClaimsIdentity(
                        [new Claim(ClaimTypes.Name, "test-user")],
                        CookieAuthenticationDefaults.AuthenticationScheme);
                    // Signed into both cookie schemes with the same test identity - most "protected"
                    // routes used across this test suite rely on the app's fallback policy (which
                    // resolves to Entra's cookie scheme), while LandingController.External is
                    // explicitly gated on the CIDM cookie scheme - one shared sign-in endpoint covers
                    // both without every calling test needing to know which scheme its route uses.
                    await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
                    await context.SignInAsync(EntraAuthenticationDefaults.CookieAuthenticationScheme, new ClaimsPrincipal(identity));
                    context.Response.StatusCode = StatusCodes.Status204NoContent;
                    return;
                }

                await nextMiddleware();
            });

            next(app);
        };
    }
}

/// <summary>
/// Shared helper so every integration test that needs an authenticated session can sign in
/// without duplicating the test-only sign-in call.
/// </summary>
public static class CdcWebTestFactoryExtensions
{
    /// <summary>Creates a client and signs it in via the test-only endpoint. Every page is
    /// authenticated by default, so most integration tests need this instead of plain
    /// <see cref="WebApplicationFactory{TEntryPoint}.CreateClient()"/>. Explicitly requests the
    /// https:// base address - <see cref="WebApplicationFactoryClientOptions.BaseAddress"/> is
    /// applied after <c>ConfigureClient</c> runs, so relying on that override alone silently leaves
    /// the client on http://, which drops any Secure-only cookie (session, auth) between requests.
    /// </summary>
    public static async Task<HttpClient> SignedInClientAsync(this WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        await client.GetAsync(CdcWebTestFactory.TestSignInPath);
        return client;
    }
}
