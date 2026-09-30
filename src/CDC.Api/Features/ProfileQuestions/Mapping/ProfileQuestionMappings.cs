using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileQuestions.Dtos;

namespace CDC.Api.Features.ProfileQuestions.Mapping;

/// <summary>Projects profile question domain entities onto the DTOs returned by the API.</summary>
public static class ProfileQuestionMappings
{
    /// <summary>Projects a profile question entity.</summary>
    /// <param name="question">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ProfileQuestionDto ToDto(this ProfileQuestion question) => new()
    {
        Id = question.Id,
        Name = question.Name,
        ShortName = question.ShortName,
        NonTechnicalName = question.NonTechnicalName,
        QuestionNumber = question.QuestionNumber,
        UserGuidance = question.UserGuidance,
        LastUpdated = question.LastUpdated
    };

    /// <summary>Projects a profile question info entity.</summary>
    /// <param name="questionInfo">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ProfileQuestionInfoDto ToDto(this ProfileQuestionInfo questionInfo) => new()
    {
        Id = questionInfo.Id,
        Name = questionInfo.Name,
        QuestionNumber = questionInfo.QuestionNumber
    };

    /// <summary>Projects a guidance report descriptor entity.</summary>
    /// <param name="report">The entity to project.</param>
    /// <returns>The equivalent DTO.</returns>
    public static ProfileGuidanceReportDto ToDto(this ProfileGuidanceReport report) => new()
    {
        ReportType = report.ReportType,
        Title = report.Title,
        IsAvailable = report.IsAvailable,
        Message = report.Message
    };
}
