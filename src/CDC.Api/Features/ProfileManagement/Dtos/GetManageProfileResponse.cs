using CDC.Common.Contracts;

namespace CDC.Api.Features.ProfileManagement.Dtos;

/// <summary>
/// The details shown on the "Manage profile" page: titles, version pointers and status. Returned
/// by <c>GET /api/profiles/{profileId}/manage</c>.
/// </summary>
public sealed record GetManageProfileResponse : ManageProfileContract; // NOSONAR
