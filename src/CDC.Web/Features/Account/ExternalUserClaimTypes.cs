namespace CDC.Web.Features.Account;

/// <summary>Claim types added to the principal once <see cref="ExternalUserResolver"/> resolves it.</summary>
public static class ExternalUserClaimTypes
{
    /// <summary>The resolved <c>[dbo].[User].Id</c>, as a string GUID.</summary>
    public const string InternalUserId = "internalUserId";

    /// <summary>The resolved <c>[dbo].[User].FullName</c>, shown in the page header.</summary>
    public const string FullName = "fullName";
}
