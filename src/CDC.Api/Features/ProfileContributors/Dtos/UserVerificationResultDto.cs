namespace CDC.Api.Features.ProfileContributors.Dtos;

using CDC.Common.Contracts;

/// <summary>DTO returned by <c>GET /api/profile-contributors/verify-username</c>.</summary>
public sealed record UserVerificationResultDto : UserVerificationResultContract; // NOSONAR
