namespace CDC.Web.Models;

/// <summary>
/// Request body for <c>PUT /api/profiles/{profileId}</c> when only the title is being changed.
/// Mirrors CDC.Api's <c>UpdateProfileAttributesCommand</c>; the scenario title and affected
/// species lists are left at their defaults, which the API treats as "no change".
/// </summary>
public sealed record UpdateProfileTitleRequest
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }

    /// <summary>The SQL Server <c>rowversion</c> read alongside the profile, so a concurrent edit can be detected.</summary>
    public required byte[] LastUpdated { get; init; }
}
