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

    /// <summary>
    /// Whether the user can author/publish profiles, from <c>[dbo].[User].IsProfileEditor</c>.
    /// <see langword="false"/> for an internal user with no matching row (legacy's "limited
    /// access" - authenticated, but with no profile-authoring privileges).
    /// </summary>
    public const string IsProfileEditor = "isProfileEditor";

    /// <summary>
    /// Whether the user is a policy profile user (contributions report only), from
    /// <c>[dbo].[User].IsPolicyProfileUser</c>. <see langword="false"/> for an internal user with
    /// no matching row.
    /// </summary>
    public const string IsPolicyProfileUser = "isPolicyProfileUser";

    /// <summary>
    /// Present, as <c>true</c>, only when an internal profile editor signed in through CIDM. It is not a role and
    /// grants nothing - it only lets the external home page remind them where to manage external users.
    /// </summary>
    public const string IsInternalProfileEditor = "isInternalProfileEditor";
}
