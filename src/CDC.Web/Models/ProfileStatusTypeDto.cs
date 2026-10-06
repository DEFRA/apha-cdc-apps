namespace CDC.Web.Models;

/// <summary>
/// A profile status a profile can be set to, as returned by <c>GET /api/profiles/status-types</c>.
/// </summary>
public sealed record ProfileStatusTypeDto
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public bool IsValidationComplete { get; init; }
}
