using CDC.Common.Contracts;

namespace CDC.Api.Features.ExternalUsers.Dtos;

/// <summary>An external user, as returned by <c>GET /api/external-users</c>.</summary>
public sealed record ExternalUserDto : ExternalUserContract; // NOSONAR
