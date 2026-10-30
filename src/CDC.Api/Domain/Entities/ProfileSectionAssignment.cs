namespace CDC.Api.Domain.Entities;

/// <summary>A profile section a contributor is assigned to, as read from <c>spgActingUser</c>.</summary>
/// <param name="ProfileId">The profile the section belongs to.</param>
/// <param name="ProfileSectionId">The section the user may edit.</param>
public sealed record ProfileSectionAssignment(Guid ProfileId, Guid ProfileSectionId);
