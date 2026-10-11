using CDC.Api.Domain.Entities;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Api.Features.ProfileSections.Mapping;
using CDC.Common.Contracts;

namespace CDC.Api.Features.ProfileSections;

/// <summary>
/// Default <see cref="IProfileSectionService"/>: reads through <see cref="IProfileSectionRepository"/>
/// and maps domain entities onto the DTOs the API returns.
/// </summary>
/// <param name="repository">Profile section data access.</param>
/// <param name="logger">Structured logger.</param>
public sealed class ProfileSectionService(IProfileSectionRepository repository, ILogger<ProfileSectionService> logger)
    : IProfileSectionService
{
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

        // No authenticated user is available yet; mirrors the same Guid.Empty placeholder
        // ProfileNoteService already uses for contribution logging until Entra ID lands.
        var clearedCount = await repository.ClearProfileVersionSectionAnswersAsync(
            profileVersionId, profileSectionId, instructions, Guid.Empty, cancellationToken);

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

        // No authenticated user is available yet; mirrors the same Guid.Empty placeholder
        // ProfileNoteService already uses for contribution logging until Entra ID lands.
        var savedCount = await repository.SaveProfileVersionSectionAnswersAsync(
            profileVersionId, profileSectionId, instructions, Guid.Empty, cancellationToken);

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
    public async Task<IReadOnlyList<ProfileReviewCommentDto>> GetProfileSectionReviewCommentsAsync(
        Guid profileVersionId,
        Guid profileSectionId,
        CancellationToken cancellationToken)
    {
        var comments = await repository.GetProfileSectionReviewCommentsAsync(profileVersionId, profileSectionId, cancellationToken);

        return [.. comments.Select(comment => new ProfileReviewCommentDto
        {
            Id = comment.Id,
            CommentDate = comment.CommentDate,
            CommentSubject = comment.CommentSubject,
            CommentReference = comment.CommentReference,
            CommentText = comment.CommentText,
            UserName = comment.UserName,
            UserFullName = comment.UserFullName,
            ParentId = comment.ParentId,
            Level = comment.Level,
            ChildCount = comment.ChildCount
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
                TechnicalReviewFrequencyMonths = revisionInfo?.TechnicalReviewFrequencyMonths,
                NextTechnicalReview = revisionInfo?.NextTechnicalReview,
                PolicyReviewFrequencyMonths = revisionInfo?.PolicyReviewFrequencyMonths,
                NextPolicyReview = revisionInfo?.NextPolicyReview
            });
        }

        return new ProfileVersionAllRevisionDatesDto { ProfileVersionId = profileVersionId, Sections = sections };
    }

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
