namespace CDC.Common.Contracts;

/// <summary>
/// Request body for <c>POST /api/external-users</c>: the details of an external user an administrator
/// pre-registers so they can sign in through CIDM. Shared by CDC.Api and CDC.Web.
/// </summary>
public sealed record PreRegisterExternalUserContract
{
    /// <summary>Gets the user's full name.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Gets the user's email address, which CIDM sign-in is matched against.</summary>
    public string EmailAddress { get; init; } = string.Empty;

    /// <summary>Gets the user's organisation.</summary>
    public string Organisation { get; init; } = string.Empty;
}
