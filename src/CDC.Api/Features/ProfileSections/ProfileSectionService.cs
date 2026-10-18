using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileReports.Interfaces;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Api.Features.ProfileSections.Mapping;
using CDC.Api.Infrastructure;
using CDC.Common.Contracts;
using FluentValidation;
using FluentValidation.Results;

namespace CDC.Api.Features.ProfileSections;

/// <summary>
/// Default <see cref="IProfileSectionService"/>: reads through <see cref="IProfileSectionRepository"/>
/// and maps domain entities onto the DTOs the API returns.
/// </summary>
/// <param name="repository">Profile section data access.</param>
/// <param name="contentRepository">Reads the questionnaire, every section's answers and the reference lists the validation needs.</param>
/// <param name="auditOptions">The temporary acting user recorded as the contributor until Entra ID supplies the signed-in user.</param>
/// <param name="logger">Structured logger.</param>
/// <param name="timeProvider">Supplies the current time for review completed dates and the future-date rule; defaults to the system clock.</param>
public sealed class ProfileSectionService(
    IProfileSectionRepository repository,
    IProfileReportContentRepository contentRepository,
    SpeciesAuditOptions auditOptions,
    ILogger<ProfileSectionService> logger,
    TimeProvider? timeProvider = null)
    : IProfileSectionService
{
    private const string CompleteStatusName = "Complete";

    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    /// <inheritdoc />
    public async Task<ProfileQuestionnaireMetadataDto> GetProfileQuestionnaireMetadataAsync(CancellationToken cancellationToken)
    {
        var metadata = await repository.GetProfileQuestionnaireMetadataAsync(cancellationToken);
        logger.RetrievedProfileQuestionnaireMetadata(metadata.Sections.Count);

        return metadata.ToDto();
    }

    /// <inheritdoc />
    public async Task<ProfileSectionAnswersDto> GetProfileSectionAnswersAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        var answers = await repository.GetProfileSectionAnswersAsync(profileVersionId, profileSectionId, cancellationToken);
        logger.RetrievedProfileSectionAnswers(profileVersionId, profileSectionId, answers.FieldValues.Count);

        return answers.ToDto();
    }

    /// <inheritdoc />
    public async Task<ProfileVersionValidationStatusDto> GetProfileVersionValidationStatusAsync(
        Guid profileVersionId,
        CancellationToken cancellationToken)
    {
        var validation = await LoadValidationAsync(profileVersionId, cancellationToken);
        var sections = new List<ProfileSectionValidationStatusDto>();

        foreach (var section in validation.Questionnaire.Sections)
        {
            var reasons = await validation.Validator.ValidateSectionAsync(section, validation.Answers[section.Id], cancellationToken);

            sections.Add(new ProfileSectionValidationStatusDto
            {
                ProfileSectionId = section.Id,
                IsValid = reasons.Count == 0,
                InvalidQuestionIds = [.. reasons.Where(reason => reason.QuestionId != Guid.Empty).Select(reason => reason.QuestionId).Distinct()],
                IrrelevantQuestionIds = ProfileSectionChangePlanner.FindIrrelevantQuestionIds(section, validation.Answers[section.Id], validation.ValuesByField)
            });
        }

        logger.RetrievedProfileVersionValidationStatus(profileVersionId, sections.Count);

        return new ProfileVersionValidationStatusDto { ProfileVersionId = profileVersionId, Sections = sections };
    }

    /// <inheritdoc />
    public async Task<ClearProfileSectionResultDto> ClearProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        byte[]? lastUpdated,
        CancellationToken cancellationToken)
    {
        var questionnaire = await contentRepository.GetQuestionnaireAsync(cancellationToken);
        var section = questionnaire.Sections.FirstOrDefault(candidate => candidate.Id == profileSectionId);

        if (section is null)
        {
            return new ClearProfileSectionResultDto { FieldsClearedCount = 0 };
        }

        var answers = await contentRepository.GetSectionAnswersAsync(profileVersionId, profileSectionId, cancellationToken);
        var version = await contentRepository.GetVersionContextAsync(profileVersionId, cancellationToken);
        var change = ProfileSectionChangePlanner.PlanClear(section, answers, version.IsWhatIfScenario, lastUpdated);

        // No authenticated user yet: the contribution is logged against the configured acting user
        // (a real [User] row, as spiProfileVersionSectionUser has a foreign key) until Entra ID lands.
        var clearedCount = await repository.ApplyProfileVersionSectionChangeAsync(
            profileVersionId, profileSectionId, change, auditOptions.AuditUserId, cancellationToken);

        logger.ClearedProfileVersionSection(profileVersionId, profileSectionId, clearedCount);

        return new ClearProfileSectionResultDto { FieldsClearedCount = clearedCount };
    }

    /// <inheritdoc />
    public async Task<ProfileSectionValidationResultDto> ValidateProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        var validation = await LoadValidationAsync(profileVersionId, cancellationToken);
        var section = validation.Questionnaire.Sections.FirstOrDefault(candidate => candidate.Id == profileSectionId);

        if (section is null)
        {
            return new ProfileSectionValidationResultDto { IsValid = true };
        }

        var reasons = await validation.Validator.ValidateSectionAsync(section, validation.Answers[section.Id], cancellationToken);

        logger.ValidatedProfileVersionSection(profileVersionId, profileSectionId, reasons.Count);

        return new ProfileSectionValidationResultDto { IsValid = reasons.Count == 0, InvalidReasons = reasons };
    }

    /// <inheritdoc />
    public async Task<SaveProfileSectionResultDto> SaveProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        IReadOnlyList<ProfileFieldAnswerInputDto> answers,
        byte[]? lastUpdated,
        CancellationToken cancellationToken)
    {
        var version = await LoadVersionAnswersAsync(profileVersionId, cancellationToken);
        var section = version.Questionnaire.Sections.FirstOrDefault(candidate => candidate.Id == profileSectionId);

        if (section is null)
        {
            return new SaveProfileSectionResultDto { FieldsSavedCount = 0 };
        }

        var change = ProfileSectionChangePlanner.PlanSave(section, version.Answers, version.Context.IsWhatIfScenario, answers, lastUpdated);

        // The legacy page saved nothing, and so did not touch the section, when no answer had changed.
        if (change.ValueChangeCount == 0)
        {
            return new SaveProfileSectionResultDto { FieldsSavedCount = 0 };
        }

        // Acting user placeholder: see ClearProfileVersionSectionAsync.
        var savedCount = await repository.ApplyProfileVersionSectionChangeAsync(
            profileVersionId, profileSectionId, change, auditOptions.AuditUserId, cancellationToken);

        logger.SavedProfileVersionSection(profileVersionId, profileSectionId, savedCount);

        return new SaveProfileSectionResultDto { FieldsSavedCount = savedCount };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ProfileSectionContributionDto>> GetProfileVersionSectionContributionsAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        var contributions = await repository.GetProfileVersionSectionContributionsAsync(profileVersionId, profileSectionId, cancellationToken);

        return [.. contributions.Select(contribution => new ProfileSectionContributionDto
        {
            UserId = contribution.UserId,
            UserName = contribution.UserName,
            OrganisationName = contribution.OrganisationName,
            FullName = contribution.FullName,
            LastContributionDate = contribution.LastContributionDate
        })];
    }

    /// <inheritdoc />
    public async Task<ProfileVersionAllRevisionDatesDto> GetProfileVersionAllRevisionDatesAsync(
        Guid profileVersionId,
        CancellationToken cancellationToken)
    {
        var metadata = await repository.GetProfileQuestionnaireMetadataAsync(cancellationToken);

        var sections = new List<ProfileSectionRevisionDatesDto>();
        foreach (var section in metadata.Sections.OrderBy(section => section.SectionNumber))
        {
            var revisionInfo = await repository.GetProfileVersionSectionRevisionInfoAsync(profileVersionId, section.Id, cancellationToken);

            sections.Add(new ProfileSectionRevisionDatesDto
            {
                ProfileSectionId = section.Id,
                SectionName = section.Name,
                SectionNumber = section.SectionNumber,
                AuthorReviewFrequencyId = revisionInfo?.AuthorReviewFrequencyId,
                VaReviewFrequencyId = revisionInfo?.VaReviewFrequencyId,
                PolicyReviewFrequencyId = revisionInfo?.PolicyReviewFrequencyId,
                NextAuthorReview = revisionInfo?.NextAuthorReview,
                NextVaReview = revisionInfo?.NextVaReview,
                NextPolicyReview = revisionInfo?.NextPolicyReview,
                AuthorReviewStatusId = revisionInfo?.AuthorReviewStatusId,
                VaReviewStatusId = revisionInfo?.VaReviewStatusId,
                PolicyReviewStatusId = revisionInfo?.PolicyReviewStatusId,
                LastUpdated = revisionInfo?.LastUpdated ?? []
            });
        }

        var frequencies = await repository.GetReviewFrequenciesAsync(cancellationToken);
        var statuses = await repository.GetReviewStatusesAsync(cancellationToken);

        return new ProfileVersionAllRevisionDatesDto
        {
            ProfileVersionId = profileVersionId,
            Sections = sections,
            ReviewFrequencies = [.. frequencies.Select(item => new ReviewLookupItemDto { Id = item.Id, Name = item.Name })],
            ReviewStatuses = [.. statuses.Select(item => new ReviewLookupItemDto { Id = item.Id, Name = item.Name })]
        };
    }

    /// <inheritdoc />
    public async Task<SaveProfileRevisionDatesResultDto> SaveProfileVersionRevisionDatesAsync(
        Guid profileVersionId,
        IReadOnlyList<ProfileSectionRevisionDatesInputDto> sections,
        CancellationToken cancellationToken)
    {
        var frequencies = await repository.GetReviewFrequenciesAsync(cancellationToken);
        var statuses = await repository.GetReviewStatusesAsync(cancellationToken);
        var validFrequencyIds = frequencies.Select(item => item.Id).ToHashSet();
        var validStatusIds = statuses.Select(item => item.Id).ToHashSet();
        var completeStatusId = statuses
            .FirstOrDefault(item => string.Equals(item.Name, CompleteStatusName, StringComparison.OrdinalIgnoreCase))?.Id;

        var failures = new List<ValidationFailure>();
        var updates = new List<ProfileSectionRevisionUpdate>();

        for (var index = 0; index < sections.Count; index++)
        {
            var input = sections[index];
            var existing = await repository.GetProfileVersionSectionRevisionInfoAsync(profileVersionId, input.ProfileSectionId, cancellationToken);

            if (existing is null)
            {
                failures.Add(new ValidationFailure($"Sections[{index}].ProfileSectionId", "The section does not exist in this profile version."));
                continue;
            }

            var sectionFailureCount = failures.Count;
            AddLookupFailures(failures, index, input, validFrequencyIds, validStatusIds);

            if (failures.Count == sectionFailureCount && HasRevisionChanges(input, existing))
            {
                updates.Add(BuildRevisionUpdate(input, existing, completeStatusId));
            }
        }

        if (failures.Count > 0)
        {
            throw new ValidationException(failures);
        }

        // Acting user placeholder: see ClearProfileVersionSectionAsync.
        var savedCount = await repository.UpdateProfileVersionSectionRevisionsAsync(
            profileVersionId, updates, auditOptions.AuditUserId, cancellationToken);

        logger.SavedProfileVersionRevisionDates(profileVersionId, savedCount, sections.Count);

        return new SaveProfileRevisionDatesResultDto { SectionsSavedCount = savedCount };
    }

    private static void AddLookupFailures(
        List<ValidationFailure> failures,
        int index,
        ProfileSectionRevisionDatesInputDto input,
        HashSet<Guid> validFrequencyIds,
        HashSet<Guid> validStatusIds)
    {
        var frequencyChecks = new (string Property, Guid? Value)[]
        {
            (nameof(input.AuthorReviewFrequencyId), input.AuthorReviewFrequencyId),
            (nameof(input.VaReviewFrequencyId), input.VaReviewFrequencyId),
            (nameof(input.PolicyReviewFrequencyId), input.PolicyReviewFrequencyId)
        };
        var statusChecks = new (string Property, Guid? Value)[]
        {
            (nameof(input.AuthorReviewStatusId), input.AuthorReviewStatusId),
            (nameof(input.VaReviewStatusId), input.VaReviewStatusId),
            (nameof(input.PolicyReviewStatusId), input.PolicyReviewStatusId)
        };

        foreach (var (property, _) in frequencyChecks.Where(check => check.Value is not null && !validFrequencyIds.Contains(check.Value.Value)))
        {
            failures.Add(new ValidationFailure($"Sections[{index}].{property}", "The review frequency is not a recognised option."));
        }

        foreach (var (property, _) in statusChecks.Where(check => check.Value is not null && !validStatusIds.Contains(check.Value.Value)))
        {
            failures.Add(new ValidationFailure($"Sections[{index}].{property}", "The review status is not a recognised option."));
        }
    }

    /// <summary>Compares dates by calendar day: the page only edits the date, so a stored time of
    /// day must not make an untouched section look changed.</summary>
    private static bool HasRevisionChanges(ProfileSectionRevisionDatesInputDto input, ProfileSectionRevisionInfo existing) =>
        input.AuthorReviewFrequencyId != existing.AuthorReviewFrequencyId
        || input.VaReviewFrequencyId != existing.VaReviewFrequencyId
        || input.PolicyReviewFrequencyId != existing.PolicyReviewFrequencyId
        || input.AuthorReviewStatusId != existing.AuthorReviewStatusId
        || input.VaReviewStatusId != existing.VaReviewStatusId
        || input.PolicyReviewStatusId != existing.PolicyReviewStatusId
        || input.NextAuthorReview?.Date != existing.NextAuthorReview?.Date
        || input.NextVaReview?.Date != existing.NextVaReview?.Date
        || input.NextPolicyReview?.Date != existing.NextPolicyReview?.Date;

    private ProfileSectionRevisionUpdate BuildRevisionUpdate(
        ProfileSectionRevisionDatesInputDto input,
        ProfileSectionRevisionInfo existing,
        Guid? completeStatusId)
    {
        var now = clock.GetLocalNow().DateTime;

        return new ProfileSectionRevisionUpdate
        {
            ProfileSectionId = input.ProfileSectionId,
            AuthorReviewFrequencyId = input.AuthorReviewFrequencyId,
            VaReviewFrequencyId = input.VaReviewFrequencyId,
            PolicyReviewFrequencyId = input.PolicyReviewFrequencyId,
            NextAuthorReview = input.NextAuthorReview,
            NextVaReview = input.NextVaReview,
            NextPolicyReview = input.NextPolicyReview,
            AuthorReviewStatusId = input.AuthorReviewStatusId,
            VaReviewStatusId = input.VaReviewStatusId,
            PolicyReviewStatusId = input.PolicyReviewStatusId,
            AuthorReviewCompleted = CompletedDate(input.AuthorReviewStatusId, existing.AuthorReviewStatusId, completeStatusId, now),
            VaReviewCompleted = CompletedDate(input.VaReviewStatusId, existing.VaReviewStatusId, completeStatusId, now),
            PolicyReviewCompleted = CompletedDate(input.PolicyReviewStatusId, existing.PolicyReviewStatusId, completeStatusId, now),
            LastUpdated = input.LastUpdated
        };
    }

    /// <summary>Legacy rule: the completed date is stamped when a review's status changes to
    /// Complete; any other state leaves it blank. The load procedure does not return the stored
    /// completed dates, so an unchanged Complete status is written back blank, as in the legacy page.</summary>
    private static DateTime? CompletedDate(Guid? newStatusId, Guid? existingStatusId, Guid? completeStatusId, DateTime now) =>
        completeStatusId is not null && newStatusId == completeStatusId && newStatusId != existingStatusId ? now : null;

    /// <summary>Loads every section's answers, not only the one being changed or validated, because an answer in one section
    /// can decide the relevance of a question in another.</summary>
    private async Task<VersionAnswers> LoadVersionAnswersAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        var questionnaire = await contentRepository.GetQuestionnaireAsync(cancellationToken);
        var answers = new Dictionary<Guid, ReportSectionAnswers>();

        foreach (var section in questionnaire.Sections)
        {
            answers[section.Id] = await contentRepository.GetSectionAnswersAsync(profileVersionId, section.Id, cancellationToken);
        }

        var context = await contentRepository.GetVersionContextAsync(profileVersionId, cancellationToken);

        return new VersionAnswers(questionnaire, answers, context);
    }

    /// <summary>Loads everything the legacy <c>Validate()</c> consulted.</summary>
    private async Task<VersionValidation> LoadValidationAsync(Guid profileVersionId, CancellationToken cancellationToken)
    {
        var version = await LoadVersionAnswersAsync(profileVersionId, cancellationToken);
        var statuses = await repository.GetReviewStatusesAsync(cancellationToken);
        var completeStatusId = statuses
            .FirstOrDefault(status => string.Equals(status.Name, CompleteStatusName, StringComparison.OrdinalIgnoreCase))?.Id ?? Guid.Empty;

        var allValues = version.Answers.Values.SelectMany(sectionAnswers => sectionAnswers.Values).ToList();

        var validator = new ProfileVersionValidator(
            contentRepository,
            allValues,
            version.Context,
            completeStatusId,
            clock.GetLocalNow().DateTime);

        return new VersionValidation(version.Questionnaire, version.Answers, validator, allValues.ToLookup(value => value.FieldId));
    }

    private sealed record VersionAnswers(
        ReportQuestionnaire Questionnaire,
        Dictionary<Guid, ReportSectionAnswers> Answers,
        ReportVersionContext Context);

    private sealed record VersionValidation(
        ReportQuestionnaire Questionnaire,
        Dictionary<Guid, ReportSectionAnswers> Answers,
        ProfileVersionValidator Validator,
        ILookup<Guid, ReportFieldValue> ValuesByField);
}
