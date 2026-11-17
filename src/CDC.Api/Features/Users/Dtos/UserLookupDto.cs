namespace CDC.Api.Features.Users.Dtos;

/// <summary>
/// A user found by email address, returned to CDC.Web so it can sign that user in without the identity provider. It carries what
/// the CIDM and Entra ID sign-ins would have resolved for the user, so one lookup serves both kinds of sign-in.
/// </summary>
public sealed record UserLookupDto
{
    /// <summary>Gets the user's identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the display name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets a value indicating whether the user can author and publish profiles.</summary>
    public required bool IsProfileEditor { get; init; }

    /// <summary>Gets a value indicating whether the user is a policy profile user (contributions report only).</summary>
    public required bool IsPolicyProfileUser { get; init; }

    /// <summary>Gets a value indicating whether the user is an internal profile editor, who signs in through CIDM.</summary>
    public required bool IsInternalProfileEditor { get; init; }
}
