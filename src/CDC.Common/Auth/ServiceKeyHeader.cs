namespace CDC.Common.Auth;

/// <summary>
/// The request header CDC.Web sends on every call to CDC.Api to prove the call comes from CDC.Web.
/// </summary>
public static class ServiceKeyHeader
{
    /// <summary>Name of the header; its value is the shared service key held in Secrets Manager.</summary>
    public const string Name = "X-Api-Key";
}
