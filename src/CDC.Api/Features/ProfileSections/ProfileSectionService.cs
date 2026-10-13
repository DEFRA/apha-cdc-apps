using CDC.Api.Domain.Entities;
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
/// <param name="auditOptions">The temporary acting user recorded as the contributor until Entra ID supplies the signed-in user.</param>
/// <param name="logger">Structured logger.</param>
/// <param name="timeProvider">Supplies the current time for review completed dates; defaults to the system clock.</param>
public sealed class ProfileSectionService(
    IProfileSectionRepository repository,
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
        var metadata = await repository.GetProfileQuestionnaireMetadataAsync(cancellationToken);
        var sections = new List<ProfileSectionValidationStatusDto>();

        foreach (var section in metadata.Sections)
        {
            var answers = await repository.GetProfileSectionAnswersAsync(profileVersionId, section.Id, cancellationToken);

            sections.Add(new ProfileSectionValidationStatusDto
            {
                ProfileSectionId = section.Id,
                IsValid = IsSectionValid(section, answers)
            });
        }

        logger.RetrievedProfileVersionValidationStatus(profileVersionId, sections.Count);

        return new ProfileVersionValidationStatusDto { ProfileVersionId = profileVersionId, Sections = sections };
    }

    /// <summary>
    /// A section passes once every mandatory, answerable field (excluding "Field Group"
    /// container fields, which hold no answer of their own) across its questions has at least
    /// one recorded value.
    /// </summary>
    private static bool IsSectionValid(ProfileSectionMetadata section, ProfileSectionAnswers answers)
    {
        var valuesByQuestion = answers.FieldValues.ToLookup(value => value.QuestionId);

        return section.Questions
            .SelectMany(question => question.Fields
                .Where(field => field.IsMandatory && !string.Equals(field.DataTypeName, "Field Group", StringComparison.OrdinalIgnoreCase))
                .Select(field => (question.Id, field.FieldNumber)))
            .All(mandatoryField => valuesByQuestion[mandatoryField.Id]
                .Any(value => value.FieldNumber == mandatoryField.FieldNumber && HasRecordedValue(value)));
    }

    private static bool HasRecordedValue(ProfileFieldValue value) =>
        value.BooleanValue is not null
        || value.ListValue is not null
        || value.DecimalValue is not null
        || value.DateValue is not null
        || !string.IsNullOrWhiteSpace(value.TextValue);

    /// <inheritdoc />
    public async Task<ClearProfileSectionResultDto> ClearProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        var section = await GetSectionMetadataAsync(profileSectionId, cancellationToken);

        if (section is null)
        {
            return new ClearProfileSectionResultDto { FieldsClearedCount = 0 };
        }

        var answers = await repository.GetProfileSectionAnswersAsync(profileVersionId, profileSectionId, cancellationToken);
        var fieldsByQuestionAndNumber = BuildFieldLookup(section);

        var instructions = answers.FieldValues
            .Where(value => fieldsByQuestionAndNumber.ContainsKey((value.QuestionId, value.FieldNumber)))
            .Select(value =>
            {
                var field = fieldsByQuestionAndNumber[(value.QuestionId, value.FieldNumber)];

                return new ProfileFieldClearInstruction
                {
                    ProfileFieldId = field.Id,
                    SpeciesId = value.SpeciesId,
                    ProfileVersionQuestionRowId = value.ProfileVersionQuestionRowId,
                    IsMultiValueList = string.Equals(field.DataTypeName, "MultiValueList", StringComparison.OrdinalIgnoreCase)
                };
            })
            .Distinct()
            .ToList();

        // No authenticated user yet: the contribution is logged against the configured acting user
        // (a real [User] row, as spiProfileVersionSectionUser has a foreign key) until Entra ID lands.
        var clearedCount = await repository.ClearProfileVersionSectionAnswersAsync(
            profileVersionId, profileSectionId, instructions, auditOptions.AuditUserId, cancellationToken);

        logger.ClearedProfileVersionSection(profileVersionId, profileSectionId, clearedCount);

        return new ClearProfileSectionResultDto { FieldsClearedCount = clearedCount };
    }

    /// <inheritdoc />
    public async Task<ProfileSectionValidationResultDto> ValidateProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        var section = await GetSectionMetadataAsync(profileSectionId, cancellationToken);

        if (section is null)
        {
            return new ProfileSectionValidationResultDto { IsValid = true, UnansweredQuestions = [] };
        }

        var answers = await repository.GetProfileSectionAnswersAsync(profileVersionId, profileSectionId, cancellationToken);
        var namesByQuestion = answers.QuestionNames.ToDictionary(name => name.Id);
        var valuesByQuestion = answers.FieldValues.ToLookup(value => value.QuestionId);

        var unansweredQuestions = section.Questions
            .Where(question => question.Fields
                .Where(field => field.IsMandatory && !string.Equals(field.DataTypeName, "Field Group", StringComparison.OrdinalIgnoreCase))
                .Any(field => !valuesByQuestion[question.Id].Any(value => value.FieldNumber == field.FieldNumber && HasRecordedValue(value))))
            .Select(question => $"{section.SectionNumber}.{question.QuestionNumber} " +
                (namesByQuestion.TryGetValue(question.Id, out var name) ? name.Name : question.ShortName))
            .ToList();

        logger.ValidatedProfileVersionSection(profileVersionId, profileSectionId, unansweredQuestions.Count);

        return new ProfileSectionValidationResultDto
        {
            IsValid = unansweredQuestions.Count == 0,
            UnansweredQuestions = unansweredQuestions
        };
    }

    /// <inheritdoc />
    public async Task<SaveProfileSectionResultDto> SaveProfileVersionSectionAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        IReadOnlyList<ProfileFieldAnswerInputDto> answers,
        CancellationToken cancellationToken)
    {
        var section = await GetSectionMetadataAsync(profileSectionId, cancellationToken);

        if (section is null || answers.Count == 0)
        {
            return new SaveProfileSectionResultDto { FieldsSavedCount = 0 };
        }

        // Only standard (non-per-species, non-repeating) questions are editable; "Field Group"
        // entries are containers for a repeating question's nested fields, not answerable fields.
        var editableFieldsById = section.Questions
            .Where(question => !question.IsPerSpecies && !question.IsRepeating)
            .SelectMany(question => question.Fields)
            .Where(field => !string.Equals(field.DataTypeName, "Field Group", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(field => field.Id);

        var instructions = answers
            .Where(answer => editableFieldsById.ContainsKey(answer.ProfileFieldId))
            .Select(answer => new ProfileFieldSaveInstruction
            {
                ProfileFieldId = answer.ProfileFieldId,
                BooleanValue = answer.BooleanValue,
                ListValue = answer.ListValue,
                MultiListValues = answer.MultiListValues,
                DecimalValue = answer.DecimalValue,
                DateValue = answer.DateValue,
                TextValue = answer.TextValue,
                IsMultiValueList = string.Equals(editableFieldsById[answer.ProfileFieldId].DataTypeName, "MultiValueList", StringComparison.OrdinalIgnoreCase)
            })
            .ToList();

        if (instructions.Count == 0)
        {
            return new SaveProfileSectionResultDto { FieldsSavedCount = 0 };
        }

        // Acting user placeholder: see ClearProfileVersionSectionAsync.
        var savedCount = await repository.SaveProfileVersionSectionAnswersAsync(
            profileVersionId, profileSectionId, instructions, auditOptions.AuditUserId, cancellationToken);

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

    private async Task<ProfileSectionMetadata?> GetSectionMetadataAsync(Guid profileSectionId, CancellationToken cancellationToken)
    {
        var metadata = await repository.GetProfileQuestionnaireMetadataAsync(cancellationToken);

        return metadata.Sections.FirstOrDefault(section => section.Id == profileSectionId);
    }

    private static Dictionary<(Guid QuestionId, int FieldNumber), ProfileFieldMetadata> BuildFieldLookup(ProfileSectionMetadata section) =>
        section.Questions
            .SelectMany(question => question.Fields.Select(field => (question.Id, field)))
            .ToDictionary(entry => (entry.Id, entry.field.FieldNumber), entry => entry.field);
}
