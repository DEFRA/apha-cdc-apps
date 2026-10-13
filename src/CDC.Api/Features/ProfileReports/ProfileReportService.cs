using CDC.Api.Application;
using CDC.Api.Domain.Common;
using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileReports.Commands;
using CDC.Api.Features.ProfileReports.Dtos;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Api.Features.ProfileReports.Mapping;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Common.Contracts;

namespace CDC.Api.Features.ProfileReports;

/// <summary>
/// Default <see cref="IProfileReportService"/>: reads and writes through
/// <see cref="IProfileReportRepository"/> for the two data-backed operations, assembles the
/// contributions report from draft history and section contributions, and returns a
/// <see cref="PendingReport"/> descriptor for every other legacy operation, none of which had a
/// stored procedure of their own - they rendered PDFs in-process via TallPDF.
/// </summary>
/// <param name="repository">Profile report data access.</param>
/// <param name="sectionRepository">Profile section data access, for section names and contributions.</param>
/// <param name="userContext">The current user's profile-authoring role flags.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ProfileReportService(
    IProfileReportRepository repository,
    IProfileSectionRepository sectionRepository,
    IUserContext userContext,
    ILogger<ProfileReportService> logger)
    : IProfileReportService
{
    private const string PendingMessage =
        "This report is not yet available. PDF generation is being migrated from TallPDF to Razor views and Playwright.";

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileVersionReportDto>> GetProfileVersionReportsAsync(
        Guid profileVersionId,
        bool isAuthenticated,
        CancellationToken cancellationToken)
    {
        var reports = await repository.GetProfileVersionReportsAsync(profileVersionId, isAuthenticated, cancellationToken);
        logger.RetrievedProfileVersionReports(reports.Count, profileVersionId);

        return [.. reports.Select(report => report.ToDto())];
    }

    /// <inheritdoc />
    public async Task<ProfileReportDataDto?> GetProfileReportDataAsync(
        Guid profileVersionId,
        Guid profileReportId,
        CancellationToken cancellationToken)
    {
        var reportData = await repository.GetProfileReportDataAsync(profileVersionId, profileReportId, cancellationToken);

        if (reportData is null)
        {
            logger.ProfileReportDataNotFound(profileReportId, profileVersionId);
            return null;
        }

        logger.RetrievedProfileReportData(profileReportId, profileVersionId);

        return reportData.ToDto();
    }

    /// <inheritdoc />
    public async Task<CreateProfileReportResultDto> CreateProfileReportAsync(
        CreateProfileReportCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var profileReportId = await repository.CreateProfileReportAsync(command, cancellationToken);
        logger.CreatedProfileReport(profileReportId, command.ProfileVersionId);

        return new CreateProfileReportResultDto { ProfileReportId = profileReportId };
    }

    /// <inheritdoc />
    public async Task<Result<ContributionsReportDto>> GetContributionsReportAsync(Guid profileId, CancellationToken cancellationToken)
    {
        // Legacy GetContributionsReportCommand.CanGetReport().
        if (!(userContext.IsProfileEditor || userContext.IsPolicyProfileUser) || userContext.IsUserManagementSystem)
        {
            logger.ContributionsReportRefused(profileId);

            return Result.Forbidden<ContributionsReportDto>("You do not have permission to get the contributions report.");
        }

        var draftVersions = await repository.GetDraftProfileVersionsAsync(profileId, cancellationToken);

        if (draftVersions.Count == 0)
        {
            return Result.NotFound<ContributionsReportDto>("The profile has no draft versions to report on.");
        }

        var metadata = await sectionRepository.GetProfileQuestionnaireMetadataAsync(cancellationToken);
        var sections = metadata.Sections.OrderBy(section => section.SectionNumber).ToList();
        var versions = new List<ContributionsReportVersionDto>();

        foreach (var version in draftVersions)
        {
            var sectionDtos = new List<ContributionsReportSectionDto>();

            foreach (var section in sections)
            {
                var contributions = await sectionRepository.GetProfileVersionSectionContributionsAsync(version.Id, section.Id, cancellationToken);

                sectionDtos.Add(new ContributionsReportSectionDto
                {
                    SectionNumber = section.SectionNumber,
                    SectionName = section.Name,
                    Contributions = [.. contributions.Select(contribution => new ProfileSectionContributionDto
                    {
                        UserId = contribution.UserId,
                        UserName = contribution.UserName,
                        OrganisationName = contribution.OrganisationName,
                        FullName = contribution.FullName,
                        LastContributionDate = contribution.LastContributionDate
                    })]
                });
            }

            versions.Add(new ContributionsReportVersionDto
            {
                ProfileVersionId = version.Id,
                FullTitle = version.FullTitle,
                VersionMajor = version.VersionMajor,
                VersionMinor = version.VersionMinor,
                IsCurrent = version.EffectiveDateTo is null,
                EffectiveDateFrom = version.EffectiveDateFrom,
                EffectiveDateTo = version.EffectiveDateTo,
                ProfileStatusName = version.ProfileStatusName,
                Sections = sectionDtos
            });
        }

        logger.RetrievedContributionsReport(profileId, versions.Count);

        // The legacy report titles itself from the newest draft version.
        return Result.Success(new ContributionsReportDto
        {
            ProfileId = profileId,
            ProfileTitle = draftVersions[0].FullTitle,
            Versions = versions
        });
    }

    /// <inheritdoc />
    public Task<ProfilePrintVersionDto> GetProfilePrintVersionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        var pending = CreatePendingReport("Profile Print Version");

        return Task.FromResult(new ProfilePrintVersionDto
        {
            ProfileVersionId = profileVersionId,
            ProfileSectionId = profileSectionId,
            Title = pending.Title,
            IsAvailable = pending.IsAvailable,
            Message = pending.Message
        });
    }

    /// <inheritdoc />
    public Task<ProfileVersionComparisonReportDto> GetProfileVersionComparisonReportAsync(
        Guid sourceVersionId,
        Guid targetVersionId,
        CancellationToken cancellationToken)
    {
        var pending = CreatePendingReport("Profile Version Comparison Report");

        return Task.FromResult(new ProfileVersionComparisonReportDto
        {
            SourceVersionId = sourceVersionId,
            TargetVersionId = targetVersionId,
            Title = pending.Title,
            IsAvailable = pending.IsAvailable,
            Message = pending.Message
        });
    }

    /// <inheritdoc />
    public Task<ProfileVersionBespokeReportDto> GetProfileVersionBespokeReportAsync(
        Guid profileVersionId,
        IReadOnlyList<string> selectedSections,
        IReadOnlyList<string> selectedQuestions,
        IReadOnlyList<string> selectedGuidance,
        string templateTitle,
        CancellationToken cancellationToken)
    {
        var pending = CreatePendingReport(templateTitle);

        return Task.FromResult(new ProfileVersionBespokeReportDto
        {
            ProfileVersionId = profileVersionId,
            Title = pending.Title,
            IsAvailable = pending.IsAvailable,
            Message = pending.Message
        });
    }

    /// <inheritdoc />
    public Task<SummaryPrioritisationReportDto> GetSummaryPrioritisationReportAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        var pending = CreatePendingReport("Summary Prioritisation Report");

        return Task.FromResult(new SummaryPrioritisationReportDto
        {
            ProfileVersionId = profileVersionId,
            Title = pending.Title,
            IsAvailable = pending.IsAvailable,
            Message = pending.Message
        });
    }

    /// <inheritdoc />
    public Task<SummaryProfileReportDto> GetSummaryProfileReportAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        var pending = CreatePendingReport("Summary Profile Report");

        return Task.FromResult(new SummaryProfileReportDto
        {
            ProfileVersionId = profileVersionId,
            Title = pending.Title,
            IsAvailable = pending.IsAvailable,
            Message = pending.Message
        });
    }

    /// <inheritdoc />
    public Task<ProfileRankingReportDto> GetProfileRankingReportAsync(
        ProfileRankingReportType reportType,
        string? nameOfFilter,
        CancellationToken cancellationToken)
    {
        var pending = CreatePendingReport($"Disease Ranking Report ({reportType})");

        return Task.FromResult(new ProfileRankingReportDto
        {
            ReportType = reportType,
            NameOfFilter = nameOfFilter,
            Title = pending.Title,
            IsAvailable = pending.IsAvailable,
            Message = pending.Message
        });
    }

    // No database call and no TallPDF dependency: the PDF payload is produced by the separate
    // Razor + Playwright pipeline once that lands. See PendingReport's remarks for why this is
    // deliberately not a byte-array response.
    private static PendingReport CreatePendingReport(string title) => new()
    {
        Title = title,
        IsAvailable = false,
        Message = PendingMessage
    };
}
