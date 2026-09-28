namespace CDC.Api.Infrastructure;

/// <summary>
/// Identifies the acting user recorded against species name/parent audit entries.
/// </summary>
/// <remarks>
/// <c>SpeciesTableAuditLog.UserId</c> is a required foreign key to <c>[User].Id</c>, so a
/// real, existing user id must be supplied - there is no "system" or nullable sentinel row.
/// Real authentication (Entra ID) is not wired up yet, so this is a temporary placeholder
/// until requests carry an authenticated caller; replace this with the caller's own user id
/// once that lands.
/// </remarks>
/// <param name="AuditUserId">The <c>[User].Id</c> recorded as the author of species changes.</param>
public sealed record SpeciesAuditOptions(Guid AuditUserId);
