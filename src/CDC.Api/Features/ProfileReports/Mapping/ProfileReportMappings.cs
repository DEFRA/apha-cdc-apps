using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileReports.Dtos;

namespace CDC.Api.Features.ProfileReports.Mapping;

/// <summary>Projects profile report domain entities onto the DTOs returned by the API.</summary>
public static class ProfileReportMappings
{
    /// <summary>Projects a profile version report entity.</summary>
    /// <param name="report">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ProfileVersionReportDto ToDto(this ProfileVersionReport report) => new()
    {
        Id = report.Id,
        ReportName = report.ReportName,
        DisplayName = report.DisplayName,
        HasPdfData = report.HasPdfData,
        FileSize = report.FileSize
    };

    /// <summary>Projects a persisted report document entity.</summary>
    /// <param name="reportData">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ProfileReportDataDto ToDto(this ProfileReportData reportData) => new()
    {
        ReportData = reportData.ReportData
    };
}
