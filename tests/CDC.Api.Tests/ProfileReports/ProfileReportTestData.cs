using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileReports.Dtos;

namespace CDC.Api.Tests.ProfileReports;

/// <summary>
/// Builders for profile report domain objects and DTOs. The entities use <c>required</c>
/// members, which AutoFixture cannot populate, so they are constructed explicitly here.
/// </summary>
internal static class ProfileReportTestData
{
    public static readonly Guid ProfileVersionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ProfileReportId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid ProfileId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid ProfileSectionId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    public static byte[] ReportBytes => [1, 2, 3, 4];

    public static ProfileVersionReport ProfileVersionReport() => new()
    {
        Id = ProfileReportId,
        ReportName = "FullProfileGUID",
        DisplayName = "Full Profile Report",
        HasPdfData = true,
        FileSize = 4096
    };

    public static ProfileVersionReportDto ProfileVersionReportDto() => new()
    {
        Id = ProfileReportId,
        ReportName = "FullProfileGUID",
        DisplayName = "Full Profile Report",
        HasPdfData = true,
        FileSize = 4096
    };
}
