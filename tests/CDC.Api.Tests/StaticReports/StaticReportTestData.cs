using CDC.Api.Domain.Entities;
using CDC.Api.Features.StaticReports.Dtos;

namespace CDC.Api.Tests.StaticReports;

/// <summary>
/// Builders for static report domain objects and DTOs. The entities use <c>required</c>
/// members, which AutoFixture cannot populate, so they are constructed explicitly here.
/// </summary>
internal static class StaticReportTestData
{
    public static readonly Guid StaticReportId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid VersionId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public static readonly DateTime EffectiveDateFrom = new(2026, 9, 22, 0, 0, 0, DateTimeKind.Utc);

    public static byte[] PdfBytes => [1, 2, 3, 4];

    public static StaticReportVersion StaticReportVersion() => new()
    {
        Id = VersionId,
        StaticReportId = StaticReportId,
        Title = "Help using D2R2 guidance",
        VersionMajor = 1,
        EffectiveDateFrom = EffectiveDateFrom,
        EffectiveDateTo = null,
        IsUserManual = true,
        IsPublic = false,
        FileSize = 4096
    };

    public static StaticReportVersionDto StaticReportVersionDto() => new()
    {
        Id = VersionId,
        StaticReportId = StaticReportId,
        Title = "Help using D2R2 guidance",
        VersionMajor = 1,
        EffectiveDateFrom = EffectiveDateFrom,
        EffectiveDateTo = null,
        IsCurrent = true,
        IsUserManual = true,
        IsPublic = false,
        FileSize = 4096
    };
}
