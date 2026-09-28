using CDC.Api.Features.ProfileReports.Commands;
using CDC.Api.Features.ProfileReports.Dtos;

namespace CDC.Api.Features.ProfileReports.Interfaces;

/// <summary>
/// Application service for the profile reports feature. Owns the mapping between domain
/// entities and the DTOs exposed over HTTP, so MediatR handlers stay thin.
/// </summary>
public interface IProfileReportService
{
    /// <summary>Gets the reports available for a profile version.</summary>
    /// <param name="profileVersionId">The profile version to read.</param>
    /// <param name="isAuthenticated">Whether the caller is authenticated, matching the legacy visibility rule.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The available reports; empty when none are defined.</returns>
    Task<IReadOnlyList<ProfileVersionReportDto>> GetProfileVersionReportsAsync(
        Guid profileVersionId,
        bool isAuthenticated,
        CancellationToken cancellationToken);

    /// <summary>Gets a previously persisted report document.</summary>
    /// <param name="profileVersionId">The profile version the report covers.</param>
    /// <param name="profileReportId">The report definition to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The persisted document, or <see langword="null"/> when none has been generated.</returns>
    Task<ProfileReportDataDto?> GetProfileReportDataAsync(Guid profileVersionId, Guid profileReportId, CancellationToken cancellationToken);

    /// <summary>Persists a generated report document.</summary>
    /// <param name="command">The report to persist.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The persisted report's identifier.</returns>
    Task<CreateProfileReportResultDto> CreateProfileReportAsync(CreateProfileReportCommand command, CancellationToken cancellationToken);

    /// <summary>Gets a profile's contribution history report descriptor.</summary>
    /// <param name="profileId">The profile to report on.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report descriptor.</returns>
    Task<ContributionsReportDto> GetContributionsReportAsync(Guid profileId, CancellationToken cancellationToken);

    /// <summary>Gets a printable profile section report descriptor.</summary>
    /// <param name="profileVersionId">The profile version to report on.</param>
    /// <param name="profileSectionId">The section to report on.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report descriptor.</returns>
    Task<ProfilePrintVersionDto> GetProfilePrintVersionAsync(Guid profileVersionId, Guid profileSectionId, CancellationToken cancellationToken);

    /// <summary>Gets a profile version comparison report descriptor.</summary>
    /// <param name="sourceVersionId">The earlier profile version being compared.</param>
    /// <param name="targetVersionId">The later profile version being compared.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report descriptor.</returns>
    Task<ProfileVersionComparisonReportDto> GetProfileVersionComparisonReportAsync(
        Guid sourceVersionId,
        Guid targetVersionId,
        CancellationToken cancellationToken);

    /// <summary>Gets a template-driven bespoke report descriptor.</summary>
    /// <param name="profileVersionId">The profile version to report on.</param>
    /// <param name="selectedSections">The sections to include.</param>
    /// <param name="selectedQuestions">The questions to include.</param>
    /// <param name="selectedGuidance">The guidance to include.</param>
    /// <param name="templateTitle">The template's display title.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report descriptor.</returns>
    Task<ProfileVersionBespokeReportDto> GetProfileVersionBespokeReportAsync(
        Guid profileVersionId,
        IReadOnlyList<string> selectedSections,
        IReadOnlyList<string> selectedQuestions,
        IReadOnlyList<string> selectedGuidance,
        string templateTitle,
        CancellationToken cancellationToken);

    /// <summary>Gets a summary prioritisation report descriptor.</summary>
    /// <param name="profileVersionId">The profile version to report on.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report descriptor.</returns>
    Task<SummaryPrioritisationReportDto> GetSummaryPrioritisationReportAsync(Guid profileVersionId, CancellationToken cancellationToken);

    /// <summary>Gets a summary profile report descriptor.</summary>
    /// <param name="profileVersionId">The profile version to report on.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report descriptor.</returns>
    Task<SummaryProfileReportDto> GetSummaryProfileReportAsync(Guid profileVersionId, CancellationToken cancellationToken);

    /// <summary>Gets a disease ranking report descriptor.</summary>
    /// <param name="reportType">The report to retrieve.</param>
    /// <param name="nameOfFilter">The named filter to apply when <paramref name="reportType"/> is <see cref="ProfileRankingReportType.All"/>.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The report descriptor.</returns>
    Task<ProfileRankingReportDto> GetProfileRankingReportAsync(ProfileRankingReportType reportType, string? nameOfFilter, CancellationToken cancellationToken);
}
