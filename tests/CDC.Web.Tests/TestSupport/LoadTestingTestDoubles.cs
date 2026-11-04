using System.Security.Claims;
using CDC.Auth.Cidm.Events;
using CDC.Auth.Entra.Events;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace CDC.Web.Tests.TestSupport;

internal sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = environmentName;

    public string ApplicationName { get; set; } = "CDC.Web.Tests";

    public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}

internal sealed class ControllableClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

// Stands in for CDC.Api-backed resolvers: records the identity-provider claims it is handed and answers with what the test set.
internal sealed class FakeIdentityResolvers : IEntraInternalUserResolver, ICidmExternalUserResolver
{
    public ClaimsPrincipal? InternalToken { get; private set; }

    public ClaimsPrincipal? ExternalToken { get; private set; }

    public EntraInternalUserResolution InternalResolution { get; set; } = EntraInternalUserResolution.Allow(new Dictionary<string, string>
    {
        ["internalUserId"] = "11111111-1111-1111-1111-111111111111",
        ["fullName"] = "Load Test Internal",
        ["authenticationProvider"] = "entra",
        ["isProfileEditor"] = bool.TrueString,
        ["isPolicyProfileUser"] = bool.FalseString
    });

    public CidmExternalUserResolution ExternalResolution { get; set; } = CidmExternalUserResolution.Allow(new Dictionary<string, string>
    {
        ["internalUserId"] = "22222222-2222-2222-2222-222222222222",
        ["fullName"] = "Load Test External",
        ["authenticationProvider"] = "cidm"
    });

    Task<EntraInternalUserResolution> IEntraInternalUserResolver.ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        InternalToken = principal;
        return Task.FromResult(InternalResolution);
    }

    Task<CidmExternalUserResolution> ICidmExternalUserResolver.ResolveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        ExternalToken = principal;
        return Task.FromResult(ExternalResolution);
    }
}
