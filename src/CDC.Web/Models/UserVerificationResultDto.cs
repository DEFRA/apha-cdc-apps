using CDC.Common.Contracts;

namespace CDC.Web.Models;

/// <summary>
/// Result of verifying a username for the "Add profile contributor" lookup step, as returned by
/// <c>GET /api/profile-contributors/verify-username</c>.
/// </summary>
public sealed record UserVerificationResultDto : UserVerificationResultContract; // NOSONAR
