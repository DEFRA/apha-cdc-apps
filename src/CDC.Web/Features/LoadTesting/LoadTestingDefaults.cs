namespace CDC.Web.Features.LoadTesting;

/// <summary>Names shared by the parts of LoadTesting.</summary>
internal static class LoadTestingDefaults
{
    /// <summary>The authentication type of an identity LoadTesting signed in.</summary>
    public const string AuthenticationType = "LoadTesting";

    /// <summary>Marks a session LoadTesting created, so the session can be ended once the bypass is off.</summary>
    public const string SessionClaimType = "loadTestSession";
}
