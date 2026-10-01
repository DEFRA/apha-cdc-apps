using CDC.Common.Contracts;

namespace CDC.Web.Models;

/// <summary>
/// One current static report or user manual as returned by <c>GET /api/static-reports</c>.
/// </summary>
public sealed record StaticReportListItemDto : StaticReportVersionContract; // NOSONAR
