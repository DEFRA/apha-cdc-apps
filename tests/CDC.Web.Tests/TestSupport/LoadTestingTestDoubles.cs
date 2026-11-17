using CDC.Web.Models;
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

// The users the LoadTesting tests look up by email address, standing in for what CDC.Api answers.
internal static class LoadTestUsers
{
    public const string InternalEmailAddress = "internal.user@example.test";

    public const string ExternalEmailAddress = "external.user@example.test";

    public static readonly Guid InternalUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public static readonly Guid ExternalUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    /// <summary>Creates an internal user: a profile editor unless told otherwise.</summary>
    public static UserLookupDto Internal(bool isProfileEditor = true, bool isPolicyProfileUser = false) => new()
    {
        Id = InternalUserId,
        FullName = "Load Test Internal",
        IsProfileEditor = isProfileEditor,
        IsPolicyProfileUser = isPolicyProfileUser,
        IsInternalProfileEditor = false
    };

    /// <summary>Creates an external user.</summary>
    public static UserLookupDto External(bool isInternalProfileEditor = false) => new()
    {
        Id = ExternalUserId,
        FullName = "Load Test External",
        IsProfileEditor = isInternalProfileEditor,
        IsPolicyProfileUser = false,
        IsInternalProfileEditor = isInternalProfileEditor
    };

    public static UserLookupResult Found(UserLookupDto user) => new(UserLookupOutcome.Found, user);

    public static UserLookupResult Refused(UserLookupOutcome outcome) => new(outcome, null);
}
