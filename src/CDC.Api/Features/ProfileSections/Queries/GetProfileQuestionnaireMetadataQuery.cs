using CDC.Api.Domain.Common;
using CDC.Api.Features.ProfileSections.Dtos;
using CDC.Api.Features.ProfileSections.Interfaces;
using MediatR;

namespace CDC.Api.Features.ProfileSections.Queries;

/// <summary>
/// Retrieves the profile questionnaire structure: the 16 fixed profile reference sections,
/// their questions, and each question's fields.
/// </summary>
public sealed record GetProfileQuestionnaireMetadataQuery : IRequest<Result<ProfileQuestionnaireMetadataDto>>;

/// <summary>
/// Handles <see cref="GetProfileQuestionnaireMetadataQuery"/>.
/// </summary>
/// <param name="profileSectionService">Profile sections application service.</param>
public sealed class GetProfileQuestionnaireMetadataQueryHandler(IProfileSectionService profileSectionService)
    : IRequestHandler<GetProfileQuestionnaireMetadataQuery, Result<ProfileQuestionnaireMetadataDto>>
{
    /// <summary>Executes the query.</summary>
    /// <param name="request">The query.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Sections, questions and fields.</returns>
    public async Task<Result<ProfileQuestionnaireMetadataDto>> Handle(
        GetProfileQuestionnaireMetadataQuery request,
        CancellationToken cancellationToken)
    {
        var metadata = await profileSectionService.GetProfileQuestionnaireMetadataAsync(cancellationToken);

        return Result.Success(metadata);
    }
}
