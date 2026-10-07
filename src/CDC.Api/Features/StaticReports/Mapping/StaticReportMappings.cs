using CDC.Api.Domain.Entities;
using CDC.Api.Features.StaticReports.Dtos;

namespace CDC.Api.Features.StaticReports.Mapping;

/// <summary>Projects static report domain entities onto the DTOs returned by the API.</summary>
public static class StaticReportMappings
{
    /// <summary>Projects a static report version entity.</summary>
    /// <param name="version">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static StaticReportVersionDto ToDto(this StaticReportVersion version) => new()
    {
        Id = version.Id,
        StaticReportId = version.StaticReportId,
        Title = version.Title,
        VersionMajor = version.VersionMajor,
        EffectiveDateFrom = version.EffectiveDateFrom,
        EffectiveDateTo = version.EffectiveDateTo,
        IsCurrent = version.IsCurrent,
        IsUserManual = version.IsUserManual,
        IsPublic = version.IsPublic,
        FileSize = version.FileSize
    };

    /// <summary>Projects a static report version's PDF content.</summary>
    /// <param name="data">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static StaticReportDataDto ToDto(this StaticReportData data) => new()
    {
        PdfData = data.PdfData,
        Title = data.Title
    };
}
