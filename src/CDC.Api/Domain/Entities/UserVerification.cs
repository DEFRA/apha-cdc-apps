namespace CDC.Api.Domain.Entities;

/// <summary>A global user row matched by username via <c>spgUserAuthorisation</c>.</summary>
public sealed record UserVerification
{
    /// <summary>Gets the user's identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Gets a value indicating whether this is a user management system account, which cannot be made a contributor.</summary>
    public required bool IsUserManagementSystem { get; init; }
}
