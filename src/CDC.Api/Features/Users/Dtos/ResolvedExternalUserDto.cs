namespace CDC.Api.Features.Users.Dtos;

/// <summary>
/// Resolved external user, returned to CDC.Web after a CIDM sign-in.
/// </summary>
public sealed record ExternalUserDto
{
    /// <summary>Gets the user's identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets the display name.</summary>
    public required string FullName { get; init; }

    /// <summary>Gets the email address.</summary>
    public required string EmailAddress { get; init; }

    /// <summary>Gets the user's organisation.</summary>
    public required string Organisation { get; init; }

    /// <summary>Gets a value indicating whether the user is an internal profile editor signed in through CIDM.</summary>
    public bool IsInternalProfileEditor { get; init; }
}
