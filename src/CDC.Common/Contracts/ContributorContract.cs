namespace CDC.Common.Contracts;

/// <summary>
/// A user who contributes to a profile: their account details and role on this profile. Returned
/// by <c>GET /api/profiles/{profileId}/contributors</c>. Shared by CDC.Api's response DTO and
/// CDC.Web's view model so the wire shape can never drift between the two.
/// </summary>
public abstract record ContributorContract
{
    /// <summary>Gets the identifier of the user's contributor record on this profile.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the user's logon username.</summary>
    public string UserName { get; init; } = string.Empty;

    /// <summary>Gets the user's full name.</summary>
    public string FullName { get; init; } = string.Empty;

    /// <summary>Gets the user's organisation.</summary>
    public string Organisation { get; init; } = string.Empty;

    /// <summary>Gets the display name of the user's role on this profile (for example "Technical author").</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>Gets the row version, used as the concurrency token when removing this contributor.</summary>
    public byte[] LastUpdated { get; init; } = [];
}
