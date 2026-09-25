using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileQuestions.Commands;
using CDC.Api.Features.ProfileQuestions.Dtos;
using CDC.Api.Features.ProfileQuestions.Interfaces;
using CDC.Api.Features.ProfileQuestions.Mapping;

namespace CDC.Api.Features.ProfileQuestions;

/// <summary>
/// Default <see cref="IProfileQuestionService"/>: reads and writes through
/// <see cref="IProfileQuestionRepository"/> and maps domain entities onto the DTOs the API
/// returns.
/// </summary>
/// <param name="repository">Profile question data access.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ProfileQuestionService(IProfileQuestionRepository repository, ILogger<ProfileQuestionService> logger)
    : IProfileQuestionService
{
    /// <summary>Report titles shown alongside <see cref="ProfileGuidanceReportDto"/>.</summary>
    private static readonly IReadOnlyDictionary<ProfileGuidanceReportType, string> ReportTitles = new Dictionary<ProfileGuidanceReportType, string>
    {
        [ProfileGuidanceReportType.All] = "Full Guidance Report",
        [ProfileGuidanceReportType.SummaryProfile] = "Summary Profile Guidance Report",
        [ProfileGuidanceReportType.SummaryPrioritisationReport] = "Summary Prioritisation Guidance Report",
        [ProfileGuidanceReportType.QaGuidanceReport] = "QA Guidance Report"
    };

    /// <inheritdoc />
    public async Task<ProfileQuestionDto?> GetProfileQuestionAsync(Guid id, CancellationToken cancellationToken)
    {
        var question = await repository.GetProfileQuestionAsync(id, cancellationToken);

        if (question is null)
        {
            logger.ProfileQuestionNotFound(id);
            return null;
        }

        logger.RetrievedProfileQuestion(id);

        return question.ToDto();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileQuestionInfoDto>> GetProfileQuestionInfoListAsync(
        Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        var questions = await repository.GetProfileQuestionInfoListAsync(profileSectionId, cancellationToken);
        logger.RetrievedProfileQuestionInfoList(questions.Count, profileSectionId);

        return [.. questions.Select(question => question.ToDto())];
    }

    /// <inheritdoc />
    public Task<ProfileGuidanceReportDto> GetProfileGuidanceReportAsync(ProfileGuidanceReportType reportType, CancellationToken cancellationToken)
    {
        // No database call and no TallPDF dependency: the PDF payload is produced by the
        // separate Razor + Playwright pipeline once that lands. See ProfileGuidanceReport's
        // remarks for why this is deliberately not a byte-array response.
        var report = new ProfileGuidanceReport
        {
            ReportType = reportType,
            Title = ReportTitles[reportType],
            IsAvailable = false,
            Message = "This report is not yet available. PDF generation is being migrated from TallPDF to Razor views and Playwright."
        };

        return Task.FromResult(report.ToDto());
    }

    /// <inheritdoc />
    public async Task<ProfileQuestionDto?> UpdateProfileQuestionAsync(UpdateProfileQuestionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var question = await repository.UpdateProfileQuestionAsync(command, cancellationToken);

        if (question is null)
        {
            return null;
        }

        logger.UpdatedProfileQuestion(command.Id);

        return question.ToDto();
    }
}
