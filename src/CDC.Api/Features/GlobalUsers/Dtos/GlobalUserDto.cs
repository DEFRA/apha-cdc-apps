using CDC.Common.Contracts;

namespace CDC.Api.Features.GlobalUsers.Dtos;

/// <summary>A global user, as returned by <c>GET /api/global-users</c>.</summary>
public sealed record GlobalUserDto : GlobalUserContract; // NOSONAR
