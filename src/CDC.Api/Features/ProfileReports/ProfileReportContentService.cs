using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Common.Contracts;

namespace CDC.Api.Features.ProfileReports;

/// <summary>
/// Default <see cref="IProfileReportContentService"/>: reads the profile version, the questionnaire
/// and every section's answers, then lets <see cref="ProfileReportSectionBuilder"/> lay them out.
/// </summary>
/// <param name="reportRepository">Reads the profile version's details.</param>
/// <param name="contentRepository">Reads the questionnaire, answers and option lists.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ProfileReportContentService(
    IProfileReportRepository reportRepository,
    IProfileReportContentRepository contentRepository,
    ILogger<ProfileReportContentService> logger)
    : IProfileReportContentService
{
    private const string ValidationCompleteStatus = "validation complete";

    /// <inheritdoc />
    public async Task<Result<ProfileReportContentDto>> GetProfileReportContentAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        var header = await reportRepository.GetProfileVersionHeaderAsync(profileVersionId, cancellationToken);

        if (header is null)
        {
            return Result.NotFound<ProfileReportContentDto>("The profile version does not exist.");
        }

        var questionnaire = await contentRepository.GetQuestionnaireAsync(cancellationToken);
        var answersBySection = new List<(ReportSection Section, ReportSectionAnswers Answers)>();

        foreach (var section in questionnaire.Sections)
        {
            answersBySection.Add((section, await contentRepository.GetSectionAnswersAsync(profileVersionId, section.Id, cancellationToken)));
        }

        // A field in one section can control the relevance of a question in another, so relevance needs every value.
        var builder = new ProfileReportSectionBuilder(contentRepository, [.. answersBySection.SelectMany(entry => entry.Answers.Values)]);
        var sections = new List<ProfileReportSectionDto>();

        foreach (var (section, answers) in answersBySection)
        {
            sections.Add(await builder.BuildAsync(section, answers, cancellationToken));
        }

        logger.BuiltProfileReportContent(profileVersionId, sections.Count);

        return Result.Success(new ProfileReportContentDto
        {
            ProfileVersionId = header.Id,
            ProfileId = header.ProfileId,
            FullTitle = header.FullTitle,
            VersionLabel = header.VersionLabel,
            IsPublished = header.IsPublished,
            EffectiveDateFrom = header.EffectiveDateFrom,
            ShowDraftWatermark = ShowsDraftWatermark(header),
            Sections = sections
        });
    }

    /// <inheritdoc />
    public async Task<Result<ProfilePrioritisationScoresDto>> GetProfilePrioritisationScoresAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        if (await reportRepository.GetProfileVersionHeaderAsync(profileVersionId, cancellationToken) is null)
        {
            return Result.NotFound<ProfilePrioritisationScoresDto>("The profile version does not exist.");
        }

        var scores = await contentRepository.GetPrioritisationScoresAsync(profileVersionId, cancellationToken);
        logger.BuiltProfilePrioritisationScores(profileVersionId, scores.CategoryScores.Count, scores.CriterionScores.Count);

        return Result.Success(scores);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<ProfileQuestionGuidanceDto>>> GetQuestionGuidanceAsync(IReadOnlyList<Guid> questionIds, CancellationToken cancellationToken)
    {
        var guidance = await contentRepository.GetQuestionGuidanceAsync([.. questionIds.Distinct()], cancellationToken);
        logger.ReadQuestionGuidance(questionIds.Count, guidance.Count);

        return Result.Success(guidance);
    }

    /// <summary>Legacy: a draft is stamped unless it is the current version at the "validation complete" status.</summary>
    private static bool ShowsDraftWatermark(ProfileVersionHeader header) =>
        !header.IsPublished
        && (!header.IsCurrent || !string.Equals(header.ProfileStatusName, ValidationCompleteStatus, StringComparison.OrdinalIgnoreCase));
}
