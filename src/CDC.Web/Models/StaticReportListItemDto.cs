namespace CDC.Web.Models;

/// <summary>
/// One current static report or user manual as returned by <c>GET /api/static-reports</c>.
/// </summary>
public sealed record StaticReportListItemDto
{
    public required Guid Id { get; init; }
    public required Guid StaticReportId { get; init; }
    public required string Title { get; init; }
    public required int VersionMajor { get; init; }
    public required DateTime EffectiveDateFrom { get; init; }
    public DateTime? EffectiveDateTo { get; init; }
    public required bool IsUserManual { get; init; }
    public required bool IsPublic { get; init; }
    public required int FileSize { get; init; }
    public bool IsCurrent => EffectiveDateTo is null;
}
