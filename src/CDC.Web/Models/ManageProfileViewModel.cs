using CDC.Common.Contracts;

namespace CDC.Web.Models;

/// <summary>
/// The details shown on the "Manage profile" page, as returned by
/// <c>GET /api/profiles/{profileId}/manage</c>.
/// </summary>
public sealed record ManageProfileViewModel : ManageProfileContract; // NOSONAR
