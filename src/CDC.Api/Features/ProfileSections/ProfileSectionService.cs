using CDC.Api.Features.ProfileSections.Dtos;
using CDC.Api.Features.ProfileSections.Interfaces;
using CDC.Api.Features.ProfileSections.Mapping;

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
}
