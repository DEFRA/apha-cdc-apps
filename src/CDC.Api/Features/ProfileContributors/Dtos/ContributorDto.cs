using CDC.Common.Contracts;

namespace CDC.Api.Features.ProfileContributors.Dtos;

/// <summary>
/// A profile contributor, as returned by <c>GET /api/profiles/{profileId}/contributors</c>.
/// </summary>
public sealed record ContributorDto : ContributorContract; // NOSONAR
