namespace CDC.Api.Features.ProfileManagement.Dtos;

/// <summary>
/// A profile status a profile can be set to. Returned by <c>GET /api/profiles/status-types</c>.
/// </summary>
public sealed record ProfileStatusTypeDto
{
    /// <summary>Gets the status identifier.</summary>
    public Guid Id { get; init; }

    /// <summary>Gets the status display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether a profile in this status requires no further validation.</summary>
    public bool IsValidationComplete { get; init; }
}
