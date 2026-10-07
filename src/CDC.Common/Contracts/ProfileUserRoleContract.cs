namespace CDC.Common.Contracts;

/// <summary>
/// A role a contributor can hold on a profile, as returned by <c>GET /api/profile-user-roles</c>.
/// Shared by CDC.Api's response DTO and CDC.Web's view model so the wire shape can never drift
/// between the two.
/// </summary>
public abstract record ProfileUserRoleContract
{
    /// <summary>Gets the role identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the role's display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether this role grants section-edit permissions (a
    /// "contributor"), as opposed to a review-only role (a "reviewer").</summary>
    public bool IsContributor { get; init; }
}
