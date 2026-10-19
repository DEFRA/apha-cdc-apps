namespace CDC.Common.Auth;

/// <summary>
/// The request header CDC.Web sends to CDC.Api to say which signed-in user a call is made for.
/// </summary>
public static class ActingUserHeader
{
    /// <summary>Name of the header; its value is the signed-in user's <c>[User].Id</c> as a GUID.</summary>
    public const string Name = "X-Acting-User-Id";
}
