using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileReports.Dtos;
using CDC.Common.Contracts;

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

    /// <summary>Projects a profile section with a review due, for the disease reviews report.</summary>
    /// <param name="section">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static DiseaseReviewsReportRowDto ToDto(this DiseaseReviewSection section) => new()
    {
        ProfileVersionId = section.ProfileVersionId,
        Title = section.Title,
        VersionMajor = section.VersionMajor,
        VersionMinor = section.VersionMinor,
        SectionNumber = section.SectionNumber,
        SectionName = section.SectionName,
        NextPolicyReview = section.NextPolicyReview,
        NextAuthorReview = section.NextAuthorReview,
        NextVaReview = section.NextVaReview
    };
}
