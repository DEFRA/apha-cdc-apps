namespace CDC.Web.Models;

/// <summary>
/// The subset of a profile's attributes needed by CDC.Web, as returned by
/// <c>GET /api/profiles/{profileId}/attributes</c>.
/// </summary>
public sealed record ProfileAttributesDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;

    /// <summary>The SQL Server <c>rowversion</c> to send back when updating.</summary>
    public byte[] LastUpdated { get; init; } = [];
}
