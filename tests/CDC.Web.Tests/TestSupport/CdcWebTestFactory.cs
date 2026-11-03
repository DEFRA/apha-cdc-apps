using System.Security.Claims;
using CDC.Auth.Cidm;
using CDC.Auth.Entra;
using CDC.Web.Authorization.Constants;
using CDC.Web.Features.Account;
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
    public const string RolesQueryKey = "roles";
    public const string ExternalOnlyQueryKey = "externalOnly";
    public const string InternalOnlyQueryKey = "internalOnly";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Program.cs reads Api:BaseUrl from builder.Configuration before builder.Build() runs, earlier
        // than ConfigureAppConfiguration below ever applies - UseSetting is the only config source that
        // lands in time. Api:BaseUrl now lives only in appsettings.Development.json, which is never
        // loaded unless ASPNETCORE_ENVIRONMENT=Development; CI runners don't set that env var (unlike
        // this machine, apparently), so the test host there defaults to Production and has no value
        // for it at all unless supplied here.
        builder.UseSetting("Api:BaseUrl", "http://cdc-api.test");

        // The session tests assume the legacy 20 and 5 minutes, not whatever the loaded appsettings carry.
        builder.UseSetting("SessionTimeout:IdleTimeout", "00:20:00");
        builder.UseSetting("SessionTimeout:WarningBeforeExpiry", "00:05:00");

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
                    var identity = new ClaimsIdentity(BuildTestClaims(context.Request.Query), CookieAuthenticationDefaults.AuthenticationScheme);
                    // Signed into both cookie schemes with the same test identity unless ?externalOnly= or ?internalOnly= leaves one out,
                    // as a real session of one identity provider would: most tests only need a signed-in user and do not mind which.
                    if (!context.Request.Query.ContainsKey(InternalOnlyQueryKey))
                    {
                        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
                    }

                    if (!context.Request.Query.ContainsKey(ExternalOnlyQueryKey))
                    {
                        await context.SignInAsync(EntraAuthenticationDefaults.CookieAuthenticationScheme, new ClaimsPrincipal(identity));
                    }
                    context.Response.StatusCode = StatusCodes.Status204NoContent;
                    return;
                }

                await nextMiddleware();
            });

            next(app);
        };

        // No query signs in as a ProfileEditor; ?roles=A,B gives exactly those roles; ?isProfileEditor=/?isPolicyProfileUser= give the resolver's permission claims.
        private static List<Claim> BuildTestClaims(IQueryCollection query)
        {
            var claims = new List<Claim> { new(ClaimTypes.Name, "test-user") };

            if (query.ContainsKey(ExternalOnlyQueryKey))
            {
                claims.Add(new Claim(ExternalUserClaimTypes.AuthenticationProvider, CidmAuthenticationDefaults.AuthenticationScheme));
            }
            else if (query.ContainsKey(InternalOnlyQueryKey))
            {
                claims.Add(new Claim(ExternalUserClaimTypes.AuthenticationProvider, EntraAuthenticationDefaults.AuthenticationScheme));
            }

            if (query.TryGetValue(RolesQueryKey, out var roles))
            {
                claims.AddRange(roles.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries).Select(role => new Claim(ClaimTypes.Role, role)));
            }

            var permissionClaimTypes = new[] { ExternalUserClaimTypes.IsProfileEditor, ExternalUserClaimTypes.IsPolicyProfileUser }
                .Where(query.ContainsKey)
                .ToList();
            claims.AddRange(permissionClaimTypes.Select(claimType => new Claim(claimType, query[claimType].ToString())));

            if (!query.ContainsKey(RolesQueryKey) && permissionClaimTypes.Count == 0)
            {
                claims.Add(new Claim(ClaimTypes.Role, AuthorizationRoles.ProfileEditor));
            }

            return claims;
        }
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
    public static async Task<HttpClient> SignedInClientAsync(this WebApplicationFactory<Program> factory) =>
        await SignInAsync(factory, string.Empty);

    /// <summary>Signs in with exactly the given role claims (none when empty), for pages whose content depends on the user's role.</summary>
    public static async Task<HttpClient> SignedInClientWithRolesAsync(this WebApplicationFactory<Program> factory, params string[] roles) =>
        await SignInAsync(factory, $"{CdcWebTestFactory.RolesQueryKey}={string.Join(',', roles)}");

    /// <summary>Signs in with the permission claims the Entra resolver issues from the user's database row and no role claim,
    /// so any role the user has comes from the claims transformation, as it does for a real internal user.</summary>
    public static async Task<HttpClient> SignedInClientWithPermissionsAsync(
        this WebApplicationFactory<Program> factory, bool isProfileEditor, bool isPolicyProfileUser) =>
        await SignInAsync(
            factory,
            $"{ExternalUserClaimTypes.IsProfileEditor}={isProfileEditor}&{ExternalUserClaimTypes.IsPolicyProfileUser}={isPolicyProfileUser}");

    /// <summary>Signs in through the CIDM cookie only, with no role, as an external user with no D2R2 permissions.
    /// Redirects are not followed, so a test can see a page sending the user to sign in elsewhere.</summary>
    public static async Task<HttpClient> SignedInExternalClientAsync(this WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await client.GetAsync($"{CdcWebTestFactory.TestSignInPath}?{CdcWebTestFactory.ExternalOnlyQueryKey}=true&{CdcWebTestFactory.RolesQueryKey}=");
        return client;
    }

    /// <summary>Signs in through the Entra ID cookie only, as a profile editor. Redirects are not followed, so a test can see a page
    /// sending the user to sign in elsewhere.</summary>
    public static async Task<HttpClient> SignedInInternalClientAsync(this WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        await client.GetAsync($"{CdcWebTestFactory.TestSignInPath}?{CdcWebTestFactory.InternalOnlyQueryKey}=true");
        return client;
    }

    private static async Task<HttpClient> SignInAsync(WebApplicationFactory<Program> factory, string query)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        await client.GetAsync(query.Length == 0 ? CdcWebTestFactory.TestSignInPath : $"{CdcWebTestFactory.TestSignInPath}?{query}");
        return client;
    }
}
