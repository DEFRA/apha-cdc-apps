namespace CDC.Web.Tests.Integration;

/// <summary>
/// Program.cs requires Api:BaseUrl to be configured (it throws at startup otherwise), and there
/// is no appsettings.Development.json checked in to supply it (it's gitignored to keep local
/// secrets out of source control). Every test that spins up WebApplicationFactory&lt;Program&gt;
/// must call this first so the host can actually start.
/// </summary>
internal static class WebTestEnvironment
{
    public static void EnsureConfigured()
    {
        Environment.SetEnvironmentVariable("Api__BaseUrl", "http://localhost:5252");
    }
}
