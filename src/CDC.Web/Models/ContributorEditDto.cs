using CDC.Common.Contracts;

namespace CDC.Web.Models;

/// <summary>
/// Full editable detail for one profile contributor, as returned by
/// <c>GET /api/profiles/{profileId}/contributors/{contributorId}</c>.
/// </summary>
public sealed record ContributorEditDto : ContributorEditContract; // NOSONAR
