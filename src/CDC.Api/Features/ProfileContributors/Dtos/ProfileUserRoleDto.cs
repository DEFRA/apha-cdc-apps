using CDC.Common.Contracts;

namespace CDC.Api.Features.ProfileContributors.Dtos;

/// <summary>A role a contributor can hold on a profile, as returned by <c>GET /api/profile-user-roles</c>.</summary>
public sealed record ProfileUserRoleDto : ProfileUserRoleContract; // NOSONAR
