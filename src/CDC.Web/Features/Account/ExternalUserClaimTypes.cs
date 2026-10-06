using CDC.Auth.Cidm;

namespace CDC.Web.Features.Account;

/// <summary>Claim types added to the principal once <see cref="ExternalUserResolver"/> resolves it.</summary>
public static class ExternalUserClaimTypes
{
    /// <summary>The resolved <c>[dbo].[User].Id</c>, as a string GUID.</summary>
    public const string InternalUserId = "internalUserId";

    /// <summary>The resolved <c>[dbo].[User].FullName</c>, shown in the page header.</summary>
    public const string FullName = "fullName";

    /// <summary>
    /// Which scheme authenticated this session (currently always <see cref="CidmAuthenticationDefaults.AuthenticationScheme"/>).
    /// Added only on successful CIDM resolution, so it - not the presence of other claims that a
    /// future internal-user identity provider might also populate - is the reliable signal for
    /// "is this an external user".
    /// </summary>
    public const string AuthenticationProvider = "authenticationProvider";
}
