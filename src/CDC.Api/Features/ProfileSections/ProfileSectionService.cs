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
}
